using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Greengrass;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;

namespace MackerelAlertToAwsIot
{
    public class MackerelAlertLampProps
    {
    }

    //https://dev.classmethod.jp/cloud/aws/aws-cdk-greengrass-rasberrypi/
    public class MackerelAlertLampStack : Stack
    {
        public IRuleTarget MaclerelAlertHandler { get; private set; }

        internal MackerelAlertLampStack(Construct scope, string id, MackerelAlertLampProps MackerelLampProps, IStackProps props = null) : base(scope, id, props)
        {
            var ggLambda = new Function(this, "MackerelAlertLampLambda", new FunctionProps()
            {
                Runtime = Runtime.PYTHON_3_7,
                Code = Code.FromAsset("handlers"),
                Handler = "MackerelAlertLampLambda.handler",
            });

            var ggLambdaAlias = new Alias(this, "MackerelAlertLampLambdaAlias", new AliasProps()
            {
                AliasName = "v1",
                Version = ggLambda.LatestVersion,
            });

            var ggCore = new CfnCoreDefinition(this, "MackerelAlertLampCore", new CfnCoreDefinitionProps()
            {
                Name = "MackerelAlertLampCore",
                // モノは手で登録する、、、だとドリフトするのか？　だとしたらだるい。。。
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

            MaclerelAlertHandler = new LambdaFunction(ggLambdaAlias.Lambda);
        }
    }
}
