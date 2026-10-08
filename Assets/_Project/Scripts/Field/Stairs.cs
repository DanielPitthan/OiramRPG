using Oiram.World;

namespace Oiram.Field
{
    /// <summary>Escada para o próximo andar da dungeon.</summary>
    public sealed class Stairs : FieldInteractable
    {
        public override string Prompt => "Descer para o próximo andar";

        public override void Interact(FieldPlayerController player) => DungeonDirector.Instance?.Descend();
    }
}
