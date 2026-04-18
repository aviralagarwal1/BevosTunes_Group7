namespace BevosTunesMVC.Models;

public class ActionConfirmationViewModel
{
    public string Title { get; set; } = "";
    public string Heading { get; set; } = "";
    public string Message { get; set; } = "";
    public string ConfirmAction { get; set; } = "";
    public string? ConfirmController { get; set; }
    public string ConfirmButtonText { get; set; } = "Confirm";
    public string ConfirmButtonClass { get; set; } = "btn-danger";
    public string CancelUrl { get; set; } = "/";
    public Dictionary<string, string> HiddenFields { get; set; } = new();
    public List<string> Details { get; set; } = new();
}
