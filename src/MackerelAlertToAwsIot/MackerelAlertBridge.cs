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

    public class MackerelAlertBridgeStack : Stack
    {
        public IEventBus AlertBus { get; private set; }

        internal MackerelAlertBridgeStack(Construct scope, string id, MackerelAlertBridgeProps props) : base(scope, id, props)
        {
            var eventBusStatement = new PolicyStatement(new PolicyStatementProps()
            {
                Actions = new string[]
                {
                    "events:CreateEventBus",
                },
                Resources = new string[]
                {
                    "*",
                },
            });
            var additionalPolicy = new ManagedPolicy(this, "IntegrationRolePolicy", new ManagedPolicyProps()
            {
                Statements = new PolicyStatement[]
                {
                    eventBusStatement,
                },
            });
            // Ref: https://mackerel.io/ja/docs/entry/integrations/aws
            var integrationRole = new Role(this, "IntegrationRole", new RoleProps()
            {
                AssumedBy = new AccountPrincipal("217452466226"),
                ExternalIds = new string[]{
                    props.StsExternalId,
                },
                ManagedPolicies = new IManagedPolicy[]
                {
                    additionalPolicy,
                },
            });
            new CfnOutput(this, "IntegrationRoleArn", new CfnOutputProps()
            {
                Value = integrationRole.RoleArn
            });

            var eventSourceName = $"aws.partner/mackerel.io/{props.OrganizationName}/{props.EventName}";
            var mackerelAlertBus = new EventBus(this, "mackerel-alert-bus", new EventBusProps()
            {
                EventSourceName = eventSourceName,
            });

            AlertBus = mackerelAlertBus;
        }
    }
}
