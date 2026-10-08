using System.Collections.Generic;
using System.Linq;
using Oiram.Core;
using UnityEditor;
using UnityEngine;

namespace Oiram.EditorTools
{
    /// <summary>Salva o conteúdo de <see cref="DefaultContent"/> como assets editáveis no Inspector.</summary>
    public static class ContentSeeder
    {
        public const string DataRoot = "Assets/_Project/Data";
        public const string DatabasePath = "Assets/_Project/Resources/GameDatabase.asset";

        [MenuItem("OiramRPG/Conteúdo/Recriar conteúdo padrão (sobrescreve)")]
        static void ReseedMenu()
        {
            if (EditorUtility.DisplayDialog("Recriar conteúdo",
                    "Isto apaga Assets/_Project/Data e recria todo o conteúdo padrão. Edições feitas nos assets serão perdidas. " +
                    "Depois rode OiramRPG ▸ Construir Fatia Vertical para atualizar as cenas.", "Recriar", "Cancelar"))
                Seed(overwrite: true);
        }

        /// <summary>Onde cada objeto do conteúdo padrão vive como asset.</summary>
        static List<(Object asset, string path)> Layout(GameDatabase db)
        {
            var list = new List<(Object, string)> { (db.balance, $"{DataRoot}/BalanceConfig.asset") };
            void All<T>(IEnumerable<T> defs, string folder) where T : Definition
            {
                foreach (var def in defs) list.Add((def, $"{DataRoot}/{folder}/{def.id}.asset"));
            }
            All(db.abilities, "Abilities");
            All(db.jobs, "Jobs");
            All(db.characters, "Characters");
            All(db.items, "Items");
            All(db.affixes, "Affixes");
            All(db.consumables, "Consumables");
            All(db.lootTables, "LootTables");
            All(db.enemies, "Enemies");
            All(db.encounters, "Encounters");
            All(db.dungeons, "Dungeons");
            All(db.locations, "Locations");
            list.Add((db, DatabasePath));
            return list;
        }

        /// <summary>Cria os assets se ainda não existirem (ou sempre, com <paramref name="overwrite"/>).</summary>
        public static GameDatabase Seed(bool overwrite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (existing != null && !overwrite) return existing;

            if (overwrite)
            {
                AssetDatabase.DeleteAsset(DataRoot);
                AssetDatabase.DeleteAsset(DatabasePath);
            }

            var db = DefaultContent.Build();
            var layout = Layout(db);
            foreach (var (asset, path) in layout)
            {
                EditorFolders.Ensure(Folder(path));
                AssetDatabase.CreateAsset(asset, path);
            }

            // Re-serializa tudo agora que todos são assets, para as referências cruzadas ficarem gravadas.
            foreach (var (asset, _) in layout) EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[OiramRPG] Conteúdo padrão criado: {layout.Count} assets.");
            return db;
        }

        [MenuItem("OiramRPG/Conteúdo/Sincronizar com o código (DefaultContent)")]
        static void SyncMenu()
        {
            if (EditorUtility.DisplayDialog("Sincronizar conteúdo",
                    "Os valores de DefaultContent.cs vão sobrescrever os assets em Assets/_Project/Data " +
                    "(edições feitas no Inspector serão perdidas). Referências nas cenas são preservadas.", "Sincronizar", "Cancelar"))
                Sync();
        }

        /// <summary>
        /// Atualiza os assets existentes com os valores de <see cref="DefaultContent"/> sem trocar os GUIDs
        /// (as cenas continuam apontando para os mesmos assets). Cria o que falta e apaga o que saiu do código.
        /// </summary>
        public static GameDatabase Sync()
        {
            var fresh = DefaultContent.Build();
            var layout = Layout(fresh);
            var map = new Dictionary<Object, Object>();
            int created = 0, updated = 0, removed = 0;

            foreach (var (asset, path) in layout)
            {
                var existing = AssetDatabase.LoadMainAssetAtPath(path);
                if (existing != null && existing.GetType() == asset.GetType())
                {
                    map[asset] = existing;
                    updated++;
                    continue;
                }
                if (existing != null) AssetDatabase.DeleteAsset(path);
                EditorFolders.Ensure(Folder(path));
                AssetDatabase.CreateAsset(asset, path);
                map[asset] = asset;
                created++;
            }

            foreach (var (asset, _) in layout)
            {
                var target = map[asset];
                if (target != asset) EditorUtility.CopySerialized(asset, target);
                Remap(target, map);
                EditorUtility.SetDirty(target);
            }

            var keep = new HashSet<string>(layout.Select(l => l.path));
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { DataRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (keep.Contains(path)) continue;
                AssetDatabase.DeleteAsset(path);
                removed++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[OiramRPG] Conteúdo sincronizado: {updated} atualizados, {created} criados, {removed} removidos.");
            return (GameDatabase)map[fresh];
        }

        /// <summary>Troca referências para objetos temporários pelas dos assets correspondentes.</summary>
        static void Remap(Object target, Dictionary<Object, Object> map)
        {
            var so = new SerializedObject(target);
            var it = so.GetIterator();
            bool changed = false;
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                if (map.TryGetValue(it.objectReferenceValue, out var replacement) && replacement != it.objectReferenceValue)
                {
                    it.objectReferenceValue = replacement;
                    changed = true;
                }
            }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }

        static string Folder(string path) => path.Substring(0, path.LastIndexOf('/'));
    }

    public static class EditorFolders
    {
        public static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }

    /// <summary>
    /// Materiais e malhas gerados por código viram assets ao "assar" as cenas (cenas só guardam referências a assets).
    /// Também cria os materiais-base em Resources (garantem os shaders no build).
    /// </summary>
    public static class EditorPalette
    {
        const string BaseFolder = "Assets/_Project/Resources/Materials";
        const string Folder = "Assets/_Project/Materials/Generated";
        const string MeshFolder = Folder + "/Meshes";

        static readonly (string file, string shader)[] Bases =
        {
            ("OiramToon", "Oiram/Toon"),
            ("OiramGlow", "Oiram/Glow"),
            ("OiramWater", "Oiram/Water"),
            ("OiramSky", "Oiram/Sky"),
        };

        public static void EnsureBaseMaterials()
        {
            EditorFolders.Ensure(BaseFolder);
            foreach (var (file, shaderName) in Bases)
            {
                string path = $"{BaseFolder}/{file}.mat";
                var shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    Debug.LogError($"[OiramRPG] Shader '{shaderName}' não encontrado (erro de compilação?).");
                    continue;
                }
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) AssetDatabase.CreateAsset(new Material(shader) { name = file }, path);
                else if (mat.shader != shader) mat.shader = shader;
            }
            // O material URP/Lit antigo não é mais usado.
            if (AssetDatabase.LoadAssetAtPath<Material>($"{BaseFolder}/OiramLit.mat") != null) AssetDatabase.DeleteAsset($"{BaseFolder}/OiramLit.mat");
        }

        static string Safe(string name) => string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_'));

        static Material PersistMaterial(Material mat)
        {
            EditorFolders.Ensure(Folder);
            string path = $"{Folder}/{Safe(mat.name)}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Mesh PersistMesh(Mesh mesh)
        {
            EditorFolders.Ensure(MeshFolder);
            string path = $"{MeshFolder}/{Safe(mesh.name)}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        public static void Begin()
        {
            EnsureBaseMaterials();
            // Recria tudo do zero: as cenas são reconstruídas logo em seguida.
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            Palette.ClearCache();
            MeshLibrary.ClearCache();
            Palette.Persist = PersistMaterial;
            MeshLibrary.Persist = PersistMesh;
        }

        public static void End()
        {
            Palette.Persist = null;
            MeshLibrary.Persist = null;
            Palette.ClearCache();
            MeshLibrary.ClearCache();
        }
    }
}
