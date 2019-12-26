using Amazon.CDK;
using Amazon.CDK.AWS.Events;

namespace MackerelAlertToAwsIot
{
    public class MackerelAlertBridgeProps : StackProps
    {
        public string OrganizationName { get; set; }
        public string EventName { get; set; }
        public IRuleTarget[] Targets { get; set; }
    }

    public class MackerelAlertBridgeStack : Stack
    {
        public IEventBus AlertBus { get; private set; }

        internal MackerelAlertBridgeStack(Construct scope, string id, MackerelAlertBridgeProps props) : base(scope, id, props)
        {
            var eventSourceName = $"aws.partner/mackerel.io/{props.OrganizationName}/{props.EventName}";
            var mackerelAlertBus = new EventBus(this, "mackerel-alert-bus", new EventBusProps()
            {
                // TODO EventSourceName にする
                EventBusName = "test",
                //EventSourceName = eventSourceName,
            });

            AlertBus = mackerelAlertBus;
        }
    }
}
