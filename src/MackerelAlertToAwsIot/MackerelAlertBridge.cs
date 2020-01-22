using Amazon.CDK;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.IAM;

namespace MackerelAlertToAwsIot
{
    public class MackerelAlertBridgeProps : StackProps
    {
        public string StsExternalId { get; set; }
        public string OrganizationName { get; set; }
        public string EventName { get; set; }
    }

    // XXX このスタックはMackerelと直接関連するリソースの管理にすることにした
    public class MackerelAlertBridgeStack : Stack
    {
        public IEventBus AlertBus { get; private set; }

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

            // XXX パートナーイベントソースに対するイベントバスはSaaS関係なしなので切り出す
            var eventSourceName = $"aws.partner/mackerel.io/{props.OrganizationName}/{props.EventName}";
            var mackerelAlertBus = new EventBus(this, "mackerel-alert-bus", new EventBusProps()
            {
                EventSourceName = eventSourceName,
            });

            AlertBus = mackerelAlertBus;
        }
    }
}
