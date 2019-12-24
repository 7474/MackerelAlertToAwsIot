using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Greengrass;

namespace MackerelAlertToAwsIot
{
    public class MackerelLampProps
    {
    }

//https://dev.classmethod.jp/cloud/aws/aws-cdk-greengrass-rasberrypi/
    public class MackerelLampStack : Stack
    {
        internal MackerelLampStack(Construct scope, string id, MackerelLampProps MackerelLampProps, IStackProps props = null) : base(scope, id, props)
        {
        }
    }
}
