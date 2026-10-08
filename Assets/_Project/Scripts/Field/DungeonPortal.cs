using Oiram.World;

namespace Oiram.Field
{
    /// <summary>Portal de saída da dungeon (na entrada: abandona a descida; após o chefe: conclui).</summary>
    public sealed class DungeonPortal : FieldInteractable
    {
        public bool completesRun;

        public override string Prompt => completesRun ? "Sair da dungeon (vitória!)" : "Sair da dungeon";

        public override void Interact(FieldPlayerController player) => DungeonDirector.Instance?.Leave(completesRun);
    }
}
