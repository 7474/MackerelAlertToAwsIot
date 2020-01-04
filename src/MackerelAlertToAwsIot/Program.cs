using Amazon.CDK;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using System.IO;
using System.Reflection;

namespace MackerelAlertToAwsIot
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .Add(new JsonConfigurationSource() { Path = @"config.json", })
                .Build();
            var env = new Environment();
            var app = new App();

            var mackerelAlertBridgeProps = LoadConfig(config, env, new MackerelAlertBridgeProps());
            var mackerelAlertBridge = new MackerelAlertBridgeStack(app, "MackerelAlertBridge", mackerelAlertBridgeProps);

            var mackerelAlertLampProps = LoadConfig(config, env, new MackerelAlertLampProps()
            {
                AlertBus = mackerelAlertBridge.AlertBus,
            });
            var mackerelAlertLamp = new MackerelAlertLampStack(app, "MackerelAlertLamp", mackerelAlertLampProps);

            app.Synth();
        }

        private static T LoadConfig<T>(IConfiguration config, IEnvironment env, T props) where T : StackProps
        {
            props.Env = env;
            var section = config.GetSection(typeof(T).Name);
            foreach (var p in typeof(T).GetProperties())
            {
                object value = section[p.Name];
                if (value == null)
                {
                    value = section.GetSection(p.Name)?.Get(p.PropertyType);
                }
                System.Console.Out.WriteLine("{0}: {1}", p.Name, value);
                if (value != null)
                {
                    p.SetValue(props, value);
                }
            }
            return props;
        }
    }
}
