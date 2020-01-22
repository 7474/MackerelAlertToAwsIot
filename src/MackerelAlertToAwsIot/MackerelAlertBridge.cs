using Amazon.CDK;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.IAM;

namespace MackerelAlertToAwsIot
{
    public class MackerelAlertBridgeProps : StackProps
    {
        public string StsExternalId { get; set; }
    }

    // Mackerel と直接関連するリソースを管理する。
    // スタック名はEventBridge通知チャンネル関連のリソースを作ろうとしていた時の名残。
    // 特にBridge成分は残っていない。
    public class MackerelAlertBridgeStack : Stack
    {
        internal MackerelAlertBridgeStack(Construct scope, string id, MackerelAlertBridgeProps props) : base(scope, id, props)
        {
            // Ref: https://mackerel.io/ja/docs/entry/integrations/aws
            var integrationRole = new Role(this, "IntegrationRole", new RoleProps()
            {
                AssumedBy = new AccountPrincipal("217452466226"),
                ExternalIds = new string[]{
                    props.StsExternalId,
                },
                ManagedPolicies = new IManagedPolicy[]
                {
                    ManagedPolicy.FromAwsManagedPolicyName("AWSLambdaReadOnlyAccess"),
                },
            });
            new CfnOutput(this, "IntegrationRoleArn", new CfnOutputProps()
            {
                Value = integrationRole.RoleArn
            });
        }
    }
}
