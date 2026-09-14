using System.Windows;
using SohailOS.Agents;
using SohailOS.AI;
using SohailOS.Core;

namespace SohailOS.App;

public partial class MainWindow : Window
{
    private readonly IOrchestrator _orchestrator;

    public MainWindow()
    {
        InitializeComponent();
        var provider = new StubAiProvider();
        var agents = Enum.GetValues<SohailModule>()
            .Select(m => (IModuleAgent)new ModuleAgent(m, provider, $"You are the SohailOS {m} module."));
        _orchestrator = new Orchestrator(agents);
    }

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(RequestBox.Text)) return;
        try
        {
            var result = await _orchestrator.HandleAsync(new UserRequest(RequestBox.Text, DateTimeOffset.UtcNow));
            ResponseBox.Text = $"[{result.Route.PrimaryModule}] {result.Content}";
        }
        catch (Exception ex)
        {
            ResponseBox.Text = $"Error: {ex.Message}";
        }
    }
}
