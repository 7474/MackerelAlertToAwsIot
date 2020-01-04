using Amazon.CDK;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.Greengrass;
using Amazon.CDK.AWS.IoT;
using Amazon.CDK.AWS.Lambda;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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

            var thingPolicy = new CfnPolicy(this, "MackerelAlertLampThingPoilcy", new CfnPolicyProps()
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
            var sha1 = new SHA1CryptoServiceProvider();
            var certs = props.ThingCerts
                   .Select(x => new
                   {
                       Arn = x,
                       Hash = BitConverter.ToString(sha1.ComputeHash(Encoding.UTF8.GetBytes(x))).Replace("-", ""),
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

            var cloudReceiveAlertFunction = new Function(this, "CloudReceiveAlert", new FunctionProps()
            {
                Runtime = Runtime.PYTHON_3_7,
                Code = Code.FromAsset("handlers/cloud"),
                Handler = "ReceiveAlert.handler",
            });

            var ggLambda = new Function(this, "DeviceReceiveAlert", new FunctionProps()
            {
                Runtime = Runtime.PYTHON_3_7,
                Code = Code.FromAsset("handlers/device"),
                Handler = "ReceiveAlert.handler",
            });

            var ggLambdaAlias = new Alias(this, "DeviceReceiveAlertAlias", new AliasProps()
            {
                AliasName = "v1",
                Version = ggLambda.LatestVersion,
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
                Id = "1",
                Name = "RaspberryPiGpioRw",
                ResourceDataContainer = new CfnResourceDefinition.ResourceDataContainerProperty()
                {
                    LocalDeviceResourceData = new CfnResourceDefinition.LocalDeviceResourceDataProperty()
                    {
                        SourcePath = "/dev/gpiomem",
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
                            Id = "1",
                            FunctionArn = ggLambdaAlias.FunctionArn,
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

            var ggConnector = new CfnConnectorDefinition(this, "MackerelAlertLampConnector", new CfnConnectorDefinitionProps()
            {
                Name = "MackerelAlertLampConnector",
                InitialVersion = new CfnConnectorDefinition.ConnectorDefinitionVersionProperty()
                {
                    Connectors = new CfnConnectorDefinition.ConnectorProperty[]{
                        new CfnConnectorDefinition.ConnectorProperty()
                        {
                            Id = "1",
                            ConnectorArn = $"arn:aws:greengrass:{this.Region}::/connectors/RaspberryPiGPIO/versions/1",
                            Parameters = new Dictionary<string, object>()
                            {
                                ["GpioMem-ResourceId"] = gpioRw.Id,
                                //["InputGpios"] = "5,6U,7D",
                                //["InputPollPeriod"] = 50,
                                // 10, 9, 11番は配置連続しているのでとりあえずそれを使う
                                ["OutputGpios"] = "9L,10L,11L",
                            }
                        },
                    },
                }
            });

            var ggGroup = new CfnGroup(this, "MackerelAlertLampGroup", new CfnGroupProps()
            {
                Name = "MackerelAlertLamp",
                InitialVersion = new CfnGroup.GroupVersionProperty()
                {
                    CoreDefinitionVersionArn = ggCore.AttrLatestVersionArn,
                    FunctionDefinitionVersionArn = ggFunction.AttrLatestVersionArn,
                    ResourceDefinitionVersionArn = ggResource.AttrLatestVersionArn,
                    ConnectorDefinitionVersionArn = ggConnector.AttrLatestVersionArn,
                },
            });
            ggGroup.AddDependsOn(ggCore);
            ggGroup.AddDependsOn(ggResource);
            ggGroup.AddDependsOn(ggFunction);
            ggGroup.AddDependsOn(ggConnector);

            var mackerelAlertRule = new Rule(this, "mackerel-alert-rule", new RuleProps()
            {
                EventBus = props.AlertBus,
                EventPattern = new EventPattern()
                {
                    // TODO わかったら書く
                    Source = new string[]{
                        "aws.partner/mackerel.io",
                    },
                },
                Targets = new IRuleTarget[] {
                    new LambdaFunction(cloudReceiveAlertFunction),
                    new LambdaFunction(ggLambda),
                },
            });
        }
    }
}
