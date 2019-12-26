using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Greengrass;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;

namespace MackerelAlertToAwsIot
{
    public class MackerelAlertLampProps : StackProps
    {
        public IEventBus AlertBus { get; set; }
    }

    // Ref: https://dev.classmethod.jp/cloud/aws/aws-cdk-greengrass-rasberrypi/
    public class MackerelAlertLampStack : Stack
    {
        internal MackerelAlertLampStack(Construct scope, string id, MackerelAlertLampProps props) : base(scope, id, props)
        {
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

            var ggCore = new CfnCoreDefinition(this, "MackerelAlertLampCore", new CfnCoreDefinitionProps()
            {
                Name = "MackerelAlertLampCore",
                // ���m�͎�œo�^����A�A�A���ƃh���t�g����̂��H�@���Ƃ����炾�邢�B�B�B
                InitialVersion = new CfnCoreDefinition.CoreDefinitionVersionProperty()
                {
                    Cores = new CfnCoreDefinition.CoreProperty[] { }
                }
            });

            //var ggResource = new CfnResourceDefinition(this, "MackerelAlertLampResource", new CfnResourceDefinitionProps()
            //{
            //    Name = "MackerelAlertLampResource",
            //    // T.B.D.
            //});

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
                                // MemorySize �� Timeout �͕K�{�ł���l�q
                                MemorySize = 65535,
                                Timeout = 10,   // �b
                            },
                        },
                    },
                },
            });

            var ggGroup = new CfnGroup(this, "MackerelAlertLampGroup", new CfnGroupProps()
            {
                Name = "MackerelAlertLamp",
                InitialVersion = new CfnGroup.GroupVersionProperty()
                {
                    CoreDefinitionVersionArn = ggCore.AttrLatestVersionArn,
                    FunctionDefinitionVersionArn = ggFunction.AttrLatestVersionArn,
                },
            });
            ggGroup.AddDependsOn(ggCore);
            //ggGroup.AddDependsOn(ggResource);
            ggGroup.AddDependsOn(ggFunction);

            var mackerelAlertRule = new Rule(this, "mackerel-alert-rule", new RuleProps()
            {
                EventBus = props.AlertBus,
                EventPattern = new EventPattern()
                {
                    // TODO �킩�����珑��
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
