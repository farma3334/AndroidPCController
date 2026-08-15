using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AndroidPCController.App.Pages;

public partial class TerminalPage : UserControl
{
    private readonly List<string> _commandHistory = new();
    private int _historyIndex = -1;

    public TerminalPage()
    {
        InitializeComponent();
    }

    private void CommandInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ViewModels.TerminalViewModel vm) return;

        if (e.Key == Key.Return)
        {
            var command = CommandInput.Text;
            if (!string.IsNullOrWhiteSpace(command))
            {
                _commandHistory.Add(command);
                _historyIndex = _commandHistory.Count;
                vm.ExecuteCommandCommand.Execute(command);
            }
            CommandInput.Text = string.Empty;
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (_historyIndex > 0)
            {
                _historyIndex--;
                CommandInput.Text = _commandHistory[_historyIndex];
                CommandInput.CaretIndex = CommandInput.Text.Length;
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            if (_historyIndex < _commandHistory.Count - 1)
            {
                _historyIndex++;
                CommandInput.Text = _commandHistory[_historyIndex];
            }
            else
            {
                _historyIndex = _commandHistory.Count;
                CommandInput.Text = string.Empty;
            }
            CommandInput.CaretIndex = CommandInput.Text.Length;
            e.Handled = true;
        }
    }
}
