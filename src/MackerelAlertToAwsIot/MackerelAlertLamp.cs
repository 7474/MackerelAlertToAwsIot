using Amazon.CDK;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.Greengrass;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.IoT;
using Amazon.CDK.AWS.Lambda;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MackerelAlertToAwsIot
{
    public class MackerelAlertLampProps : StackProps
    {
        public IEventBus AlertBus { get; set; }
        public string[] ThingCerts { get; set; }
    }

    // Ref: https://dev.classmethod.jp/cloud/aws/aws-cdk-greengrass-rasberrypi/
    public class MackerelAlertLampStack : Stack
    {
        internal MackerelAlertLampStack(Construct scope, string id, MackerelAlertLampProps props) : base(scope, id, props)
        {
            System.Console.Out.WriteLine(string.Join(",", props.ThingCerts));

            var thingPolicy = new Amazon.CDK.AWS.IoT.CfnPolicy(this, "MackerelAlertLampThingPoilcy", new Amazon.CDK.AWS.IoT.CfnPolicyProps()
            {
                PolicyName = "MackerelAlertLampThingPoilcy",
                PolicyDocument = new Dictionary<string, object>
                {
                    ["Version"] = "2012-10-17",
                    ["Statement"] = new object[] {
                        new Dictionary<string, object>
                        {
                            ["Effect"] = "Allow",
                            ["Action"] = new string[] {
                                "iot:*",
                                "greengrass:*",
                            },
                            ["Resource"] = new string[] {
                                "*"
                            },
                        }
                    }
                }
            });

            // IDなどに使うために証明書のARNを加工しておく。
            var certs = props.ThingCerts
                   .Select(x => new
                   {
                       Arn = x,
                       Hash = Utils.ToHash(x),
                   }).ToList();
            var certAttaches = certs.Select(x =>
            {
                var attach = new CfnPolicyPrincipalAttachment(this, "MackerelAlertLampCertAttach-" + x.Hash, new CfnPolicyPrincipalAttachmentProps()
                {
                    PolicyName = thingPolicy.PolicyName,
                    Principal = x.Arn,
                });
                attach.AddDependsOn(thingPolicy);
                return attach;
            }).ToList();

            var things = certs.Select(x => new
            {
                CertArn = x.Arn,
                Thing = new CfnThing(this, "MackerelAlertLampThing-" + x.Hash, new CfnThingProps()
                {
                    ThingName = "MackerelAlertLamp-" + x.Hash,
                })
            }).ToList();
            var thingAttaches = things.Select(x =>
            {
                var attach = new CfnThingPrincipalAttachment(this, x.Thing.ThingName + "Attach", new CfnThingPrincipalAttachmentProps()
                {
                    ThingName = x.Thing.ThingName,
                    Principal = x.CertArn,
                });
                attach.AddDependsOn(x.Thing);
                return attach;
            }).ToList();

            var mackerelAlertTopic = props.AlertBus.EventSourceName;
            var cloudReceiveAlertFunction = new Function(this, "CloudReceiveAlert", new FunctionProps()
            {
                Runtime = Runtime.PYTHON_3_7,
                Code = Code.FromAsset("handlers/cloud"),
                Handler = "ReceiveAlert.handler",
                Environment = new Dictionary<string, string>()
                {
                    ["MACKEREL_ALERT_TOPIC"] = mackerelAlertTopic,
                },
            });
            cloudReceiveAlertFunction.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps()
            {
                Actions = new string[]
                {
                    "iot:Publish",
                },
                Resources = new string[]
                {
                    "*",
                },
            }));

            var ggLambda = new Function(this, "DeviceReceiveAlert", new FunctionProps()
            {
                Runtime = Runtime.PYTHON_3_7,
                Code = Code.FromAsset("handlers/device"),
                Handler = "ReceiveAlert.handler",
            });
            var ggLambdaVersion = ggLambda.AddVersion("v1");
            var ggLambdaAlias = new Alias(this, "DeviceReceiveAlertAlias", new AliasProps()
            {
                AliasName = "v" + ggLambdaVersion.Version,
                Version = ggLambdaVersion,
            });

            var toggleGpio = new Function(this, "DeviceToggleGpio", new FunctionProps()
            {
                Runtime = Runtime.PYTHON_3_7,
                Code = Code.FromAsset("handlers/device"),
                Handler = "ToggleGpio.handler",
            });
            var toggleGpioVersion = toggleGpio.AddVersion("v1");
            var toggleGpioAlias = new Alias(this, "DeviceToggleGpioAlias", new AliasProps()
            {
                AliasName = "v" + toggleGpioVersion.Version,
                Version = toggleGpioVersion,
            });

            var ggCoreId = 0;
            var ggCore = new CfnCoreDefinition(this, "MackerelAlertLampCore", new CfnCoreDefinitionProps()
            {
                Name = "MackerelAlertLampCore",
                InitialVersion = new CfnCoreDefinition.CoreDefinitionVersionProperty()
                {
                    Cores = things.Select(x => new CfnCoreDefinition.CoreProperty()
                    {
                        Id = (++ggCoreId).ToString(),
                        CertificateArn = x.CertArn,
                        // XXX ARN参照できないの？
                        //ThingArn = x.Thing.GetAtt("Arn").Reference.ToString(),
                        //ThingArn = x.Thing.GetAtt("resource.arn").Reference.ToString(),
                        ThingArn = $"arn:aws:iot:{this.Region}:{this.Account}:thing/{x.Thing.ThingName}",
                    }).ToArray(),
                }
            });
            things.ForEach(x =>
            {
                ggCore.AddDependsOn(x.Thing);
            });

            var gpioRw = new CfnResourceDefinition.ResourceInstanceProperty()
            {
                Id = "gpio-rw",
                Name = "RaspberryPiGpioRw",
                ResourceDataContainer = new CfnResourceDefinition.ResourceDataContainerProperty()
                {
                    LocalDeviceResourceData = new CfnResourceDefinition.LocalDeviceResourceDataProperty()
                    {
                        SourcePath = "/dev/gpiomem",
                        GroupOwnerSetting = new CfnResourceDefinition.GroupOwnerSettingProperty()
                        {
                            AutoAddGroupOwner = true,
                        },
                    }
                },
            };
            var ggResource = new CfnResourceDefinition(this, "MackerelAlertLampResource", new CfnResourceDefinitionProps()
            {
                Name = "MackerelAlertLampResource",
                InitialVersion = new CfnResourceDefinition.ResourceDefinitionVersionProperty()
                {
                    Resources = new CfnResourceDefinition.ResourceInstanceProperty[]
                    {
                        gpioRw,
                    }
                },
            });

            var ggFunction = new CfnFunctionDefinition(this, "MackerelAlertLampFunction", new CfnFunctionDefinitionProps()
            {
                Name = "MackerelAlertLampFunction",
                InitialVersion = new CfnFunctionDefinition.FunctionDefinitionVersionProperty()
                {
                    Functions = new CfnFunctionDefinition.FunctionProperty[]
                    {
                        new CfnFunctionDefinition.FunctionProperty(){
                            Id = ggLambda.FunctionName + "-" + ggLambdaAlias.AliasName,
                            FunctionArn = ggLambdaAlias.FunctionArn,
                            FunctionConfiguration = new CfnFunctionDefinition.FunctionConfigurationProperty()
                            {
                                // MemorySize と Timeout は必須である様子
                                MemorySize = 65535,
                                Timeout = 10,   // 秒
                            },
                        },
                        new CfnFunctionDefinition.FunctionProperty(){
                            Id = toggleGpio.FunctionName + "-" + toggleGpioAlias.AliasName,
                            FunctionArn = toggleGpioAlias.FunctionArn,
                            FunctionConfiguration = new CfnFunctionDefinition.FunctionConfigurationProperty()
                            {
                                // MemorySize と Timeout は必須である様子
                                MemorySize = 65535,
                                Timeout = 10,   // 秒
                            },
                        },
                    },
                },
            });

            // https://docs.aws.amazon.com/ja_jp/greengrass/latest/developerguide/raspberrypi-gpio-connector.html
            var gpioConnector = new CfnConnectorDefinition.ConnectorProperty()
            {
                Id = "gpio-connector",
                ConnectorArn = $"arn:aws:greengrass:{this.Region}::/connectors/RaspberryPiGPIO/versions/1",
                Parameters = new Dictionary<string, object>()
                {
                    ["GpioMem-ResourceId"] = gpioRw.Id,
                    //["InputGpios"] = "5,6U,7D",
                    //["InputPollPeriod"] = 50,
                    // 10, 9, 11番は配置連続しているのでとりあえずそれを使う
                    ["OutputGpios"] = "9L,10L,11L",
                }
            };
            var ggConnector = new CfnConnectorDefinition(this, "MackerelAlertLampConnector", new CfnConnectorDefinitionProps()
            {
                Name = "MackerelAlertLampConnector",
                InitialVersion = new CfnConnectorDefinition.ConnectorDefinitionVersionProperty()
                {
                    Connectors = new CfnConnectorDefinition.ConnectorProperty[]{
                        gpioConnector,
                    },
                }
            });

            var ggSubscriptions = new CfnSubscriptionDefinition.SubscriptionProperty[]
                {
                    // ReceiveAlert Cloud to Device
                    new CfnSubscriptionDefinition.SubscriptionProperty()
                    {
                        Id = "mackerel-alert-to-device",
                        Source = "cloud",
                        Target = ggLambdaAlias.FunctionArn,
                        Subject = mackerelAlertTopic,
                    },
                    new CfnSubscriptionDefinition.SubscriptionProperty()
                    {
                        Id = "mackerel-alert-gpio-write-11",
                        Source = ggLambdaAlias.FunctionArn,
                        Target = gpioConnector.ConnectorArn,
                        Subject ="gpio/+/11/write",
                    },
                    // XXX Currently, when you create a subscription that uses the Raspberry Pi GPIO connector, you must specify a value for at least one of the + wildcards in the topic.
                    new CfnSubscriptionDefinition.SubscriptionProperty()
                    {
                        Id = "gpio-read",
                        Source = "cloud",
                        Target = gpioConnector.ConnectorArn,
                        Subject ="gpio/+/9/read",
                    },
                    new CfnSubscriptionDefinition.SubscriptionProperty()
                    {
                        Id = "gpio-write",
                        Source = "cloud",
                        Target = gpioConnector.ConnectorArn,
                        Subject ="gpio/+/9/write",
                    },
                    new CfnSubscriptionDefinition.SubscriptionProperty()
                    {
                        Id = "gpio-state",
                        Source = gpioConnector.ConnectorArn,
                        Target = "cloud",
                        Subject ="gpio/+/9/state",
                    },
                    new CfnSubscriptionDefinition.SubscriptionProperty()
                    {
                        Id = "gpio-error",
                        Source = gpioConnector.ConnectorArn,
                        Target = "cloud",
                        Subject ="gpio/+/error",
                    },
                    //
                    new CfnSubscriptionDefinition.SubscriptionProperty()
                    {
                        Id = "gpio-read-10",
                        Source = "cloud",
                        Target = gpioConnector.ConnectorArn,
                        Subject ="gpio/+/10/read",
                    },
                    new CfnSubscriptionDefinition.SubscriptionProperty()
                    {
                        Id = "gpio-write-10",
                        Source = "cloud",
                        Target = gpioConnector.ConnectorArn,
                        Subject ="gpio/+/10/write",
                    },
                    new CfnSubscriptionDefinition.SubscriptionProperty()
                    {
                        Id = "gpio-state-10",
                        Source = gpioConnector.ConnectorArn,
                        Target = "cloud",
                        Subject ="gpio/+/10/state",
                    },
                    new CfnSubscriptionDefinition.SubscriptionProperty()
                    {
                        Id = "gpio-read-11",
                        Source = toggleGpioAlias.FunctionArn,
                        Target = gpioConnector.ConnectorArn,
                        Subject ="gpio/+/11/read",
                    },
                    new CfnSubscriptionDefinition.SubscriptionProperty()
                    {
                        Id = "gpio-write-11",
                        Source = toggleGpioAlias.FunctionArn,
                        Target = gpioConnector.ConnectorArn,
                        Subject ="gpio/+/11/write",
                    },
                    new CfnSubscriptionDefinition.SubscriptionProperty()
                    {
                        Id = "gpio-state-11",
                        Source = gpioConnector.ConnectorArn,
                        Target = "cloud",
                        Subject ="gpio/+/11/state",
                    },
                    new CfnSubscriptionDefinition.SubscriptionProperty()
                    {
                        Id = "gpio-test",
                        Source = "cloud",
                        Target = toggleGpioAlias.FunctionArn,
                        Subject ="gpio/test",
                    },
                };
            var ggSubscription = new CfnSubscriptionDefinition(this, "MackerelAlertLampSubscription", new CfnSubscriptionDefinitionProps()
            {
                Name = "MackerelAlertLampSubscription",
                InitialVersion = new CfnSubscriptionDefinition.SubscriptionDefinitionVersionProperty()
                {
                    Subscriptions = ggSubscriptions,
                },
            });
            // Group以外のバージョンも管理しようとするとARN取るのが良く分らん。。。
            // var ggLatestSubscription = new CfnSubscriptionDefinitionVersion(this,
            //     "MackerelAlertLampSubscriptionVersion-" + Utils.ToHash(string.Join("-", ggSubscriptions.Select(x => x.Id))),
            //     new CfnSubscriptionDefinitionVersionProps()
            //     {
            //         SubscriptionDefinitionId = ggSubscription.AttrId,
            //         Subscriptions = ggSubscriptions,
            //     });

            var ggGroup = new Amazon.CDK.AWS.Greengrass.CfnGroup(this, "MackerelAlertLampGroup", new Amazon.CDK.AWS.Greengrass.CfnGroupProps()
            {
                Name = "MackerelAlertLamp",
                // XXX 引数にする
                RoleArn = "arn:aws:iam::854403262515:role/service-role/Greengrass_ServiceRole",
            });
            var ggVersionHash = Utils.ToHash(string.Join("-",
                    ggCore.AttrLatestVersionArn,
                    ggFunction.AttrLatestVersionArn,
                    ggResource.AttrLatestVersionArn,
                    ggConnector.AttrLatestVersionArn,
                    ggSubscription.AttrLatestVersionArn));
            var ggLatestVersion = new CfnGroupVersion(this, "MackerelAlertLampGroupVersion-" + ggVersionHash, new CfnGroupVersionProps()
            {
                GroupId = ggGroup.AttrId,
                CoreDefinitionVersionArn = ggCore.AttrLatestVersionArn,
                FunctionDefinitionVersionArn = ggFunction.AttrLatestVersionArn,
                ResourceDefinitionVersionArn = ggResource.AttrLatestVersionArn,
                ConnectorDefinitionVersionArn = ggConnector.AttrLatestVersionArn,
                SubscriptionDefinitionVersionArn = ggSubscription.AttrLatestVersionArn,
            });
            ggLatestVersion.AddDependsOn(ggGroup);
            ggLatestVersion.AddDependsOn(ggCore);
            ggLatestVersion.AddDependsOn(ggResource);
            ggLatestVersion.AddDependsOn(ggFunction);
            ggLatestVersion.AddDependsOn(ggConnector);
            ggLatestVersion.AddDependsOn(ggSubscription);

            var mackerelAlertRule = new Rule(this, "mackerel-alert-rule", new RuleProps()
            {
                EventBus = props.AlertBus,
                EventPattern = new EventPattern()
                {
                    Account = new string[]{
                        this.Account,
                    },
                },
                Targets = new IRuleTarget[] {
                    new LambdaFunction(cloudReceiveAlertFunction),
                },
            });
        }
    }
}
