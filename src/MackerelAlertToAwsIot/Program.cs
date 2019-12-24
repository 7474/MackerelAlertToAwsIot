using Amazon.CDK;

namespace MackerelAlertToAwsIot
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();
            new MackerelAlertBridgeStack(app, "MackerelAlertToAwsIot", new MackerelAlertBridgeProps()
            {
                OrganizationName = "koudenpa-1",
                EventName = "trial_alerts",
                Targets = new Amazon.CDK.AWS.Events.IRuleTarget[] { },
            });

            // TODO Lambda なりで仲介するならする。面倒臭い。最終的にAWS IoTのトピックに入ればいい

            // TODO GreengrassとEventを受けてLチカさせるLambda
            // https://dev.classmethod.jp/cloud/aws/aws-cdk-greengrass-rasberrypi/
            // Lambdaでソフトトークで音声合成は非現実的かなぁ？　ちょっとよくわからない。
            app.Synth();
        }
    }
}
