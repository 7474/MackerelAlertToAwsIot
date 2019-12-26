using Amazon.CDK;

namespace MackerelAlertToAwsIot
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var env = new Environment()
            {
            };
            var app = new App();

            var mackerelAlertBridge = new MackerelAlertBridgeStack(app, "MackerelAlertBridge", new MackerelAlertBridgeProps()
            {
                Env = env,
                OrganizationName = "koudenpa-1",
                EventName = "trial_alerts",
                Targets = new Amazon.CDK.AWS.Events.IRuleTarget[] {
                    //mackerelAlertLamp.MaclerelAlertHandler,
                },
            });

            var mackerelAlertLamp = new MackerelAlertLampStack(app, "MackerelAlertLamp", new MackerelAlertLampProps()
            {
                Env = env,
                AlertBus = mackerelAlertBridge.AlertBus,
            });


            app.Synth();
        }
    }
}
