using System.Collections.Generic;
using Oiram.Core;
using UnityEngine;

namespace Oiram.World
{
    public enum LocationKind
    {
        Field,
        Town,
        Dungeon,
    }

    /// <summary>Um ponto no mapa-múndi (estilo Mario RPG): campo, cidade ou dungeon.</summary>
    [CreateAssetMenu(menuName = "OiramRPG/Location", fileName = "Location")]
    public sealed class LocationDefinition : Definition
    {
        public LocationKind kind;
        [Tooltip("Cena carregada ao entrar (dungeons usam a cena Dungeon).")]
        public string sceneName;
        [Tooltip("Posição no mapa-múndi (x, z em unidades do mundo).")]
        public Vector2 mapPosition;
        public List<LocationDefinition> connections = new();
        [Tooltip("Precisa ter concluído este local antes (vencer o chefe). Vazio = liberado.")]
        public LocationDefinition requires;
        public DungeonDefinition dungeon;
        public Color color = Color.white;

        public bool IsDungeon => kind == LocationKind.Dungeon && dungeon != null;
    }
}
