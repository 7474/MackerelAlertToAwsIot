using Amazon.CDK;

namespace MackerelAlertToAwsIot
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();

            var mackerelAlertLamp = new MackerelAlertLampStack(app, "MackerelAlertLamp", new MackerelAlertLampProps()
            {
            });

            var mackerelAlertBridge = new MackerelAlertBridgeStack(app, "MackerelAlertBridge", new MackerelAlertBridgeProps()
            {
                OrganizationName = "koudenpa-1",
                EventName = "trial_alerts",
                Targets = new Amazon.CDK.AWS.Events.IRuleTarget[] {
                    //mackerelAlertLamp.MaclerelAlertHandler,
                },
            });
            mackerelAlertLamp.MaclerelAlertHandler.Bind(mackerelAlertBridge.AlertRule);

            app.Synth();
        }
    }
}
