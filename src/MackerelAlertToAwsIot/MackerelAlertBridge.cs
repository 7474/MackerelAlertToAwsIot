using Amazon.CDK;
using Amazon.CDK.AWS.Events;

namespace MackerelAlertToAwsIot
{
    public class MackerelAlertBridgeProps
    {
        public string OrganizationName { get; set; }
        public string EventName { get; set; }
        public IRuleTarget[] Targets { get; set; }
    }

    public class MackerelAlertBridgeStack : Stack
    {
        internal MackerelAlertBridgeStack(Construct scope, string id, MackerelAlertBridgeProps mackerelAlertBridgeProps, IStackProps props = null) : base(scope, id, props)
        {
            var eventSourceName = $"aws.partner/mackerel.io/{mackerelAlertBridgeProps.OrganizationName}/{mackerelAlertBridgeProps.EventName}";
            var mackerelAlertBus = new EventBus(this, "mackerel-alert-bus", new EventBusProps()
            {
                EventSourceName = eventSourceName,
            });

            var mackerelAlertRule = new Rule(this, "mackerel-alert-rule", new RuleProps()
            {
                EventBus = mackerelAlertBus,
                EventPattern = new EventPattern()
                {
                    Source = new string[]{
                        "aws.partner/mackerel.io",
                    },
                    // TODO わかったら書く
                },
                Targets = mackerelAlertBridgeProps.Targets,
            });
        }
    }
}
