using UnityEngine;

namespace Oiram.Battle
{
    /// <summary>Cores da arena de batalha (as dungeons pintam a arena com o seu tema).</summary>
    public struct BattleTheme
    {
        public Color Ground;
        public Color Cliff;
        public Color Sky;
        public Color Ambient;
        /// <summary>Ambiente fechado: esconde as árvores da arena.</summary>
        public bool Indoor;
    }
}
