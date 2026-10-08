using Oiram.UI;
using Oiram.World;

namespace Oiram.Field
{
    /// <summary>Balcão de loja da cidade: abre o <see cref="ShopMenu"/> do tipo configurado.</summary>
    public sealed class ShopKeeper : FieldInteractable
    {
        public ShopKind kind;
        public string shopName;
        public string townId;

        public override string Prompt => kind switch
        {
            ShopKind.Consumables => $"Comprar — {shopName}",
            ShopKind.Blacksmith => $"Ferreiro — {shopName}",
            ShopKind.Gambler => $"Apostar — {shopName}",
            ShopKind.Inn => $"Pousada — {shopName}",
            _ => shopName,
        };

        public override void Interact(FieldPlayerController player) => ShopMenu.Open(kind, townId, shopName);
    }
}
