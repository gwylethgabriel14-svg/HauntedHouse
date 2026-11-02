public interface IInteractable
{
    string Prompt { get; }
    void Interact(PlayerController player);
}
