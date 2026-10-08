using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Oiram.Audio;
using Oiram.Characters;
using Oiram.Core;
using Oiram.Inventory;
using Oiram.Loot;
using Oiram.Stats;
using Oiram.UI;
using UnityEngine;

namespace Oiram.Battle
{
    public enum BattleResult
    {
        Victory,
        Defeat,
        Fled,
    }

    public sealed class BattleRequest
    {
        public EncounterDefinition Encounter;
        public bool FirstStrike;
        /// <summary>Quem escolhe os comandos da party (null = jogador via menus).</summary>
        public IBattleCommandSource Commands;
        /// <summary>Pula as esperas por Confirmar nas telas de resultado (testes automáticos).</summary>
        public bool AutoAdvance;
        /// <summary>Cores da arena (dungeons). Null = arena padrão do Vale.</summary>
        public BattleTheme? Theme;
    }

    public sealed class BattleOutcome
    {
        public BattleResult Result;
        public LootDrop Loot = new();
    }

    public interface IBattleCommandSource
    {
        Awaitable<BattleCommand> Choose(BattleManager battle, BattleUnit actor, CancellationToken ct);
    }

    /// <summary>
    /// Conduz a batalha por turnos estilo Mario RPG: ordem por velocidade, comandos, timed hits
    /// (ataque e bloqueio), status, PE compartilhado e loot ao vencer.
    /// </summary>
    public sealed class BattleManager : MonoBehaviour
    {
        [SerializeField] Camera battleCamera;
        [SerializeField] Transform[] partyAnchors = Array.Empty<Transform>();
        [SerializeField] Transform[] enemyAnchors = Array.Empty<Transform>();
        [Tooltip("Encontro usado ao dar Play direto nesta cena (para testar).")]
        [SerializeField] string standaloneEncounterId = "enc_slimes2";

        readonly List<BattleUnit> units = new();
        readonly Dictionary<BattleUnit, BattleUnitView> views = new();
        readonly List<Transform> markers = new();
        readonly List<ItemInstance> stolen = new();
        LootDrop loot;
        EncounterDefinition encounter;
        bool autoAdvance;

        // Números da batalha para o log de playtest.
        int rounds, damageDealt, damageTaken, knockOuts;
        float startedAt;

        // Tremor de câmera (tempo real: continua durante o hit-stop).
        Vector3 cameraHome;
        float shakeUntil, shakeDuration, shakeMagnitude;

        void Tally(BattleUnit target, int damage)
        {
            if (target.Side == Side.Party) damageTaken += damage;
            else damageDealt += damage;
        }

        public bool StartedExternally { get; set; }
        public bool IsRunning { get; private set; }
        public BattleOutcome LastOutcome { get; private set; }
        public GameSession Session { get; private set; }
        public BattleRules Rules { get; private set; }
        public BattleHud Hud { get; private set; }
        public IReadOnlyList<BattleUnit> Units => units;
        public bool CanFlee => encounter != null && encounter.canFlee;
        public Camera Camera => battleCamera;

        public void Configure(Camera camera, Transform[] party, Transform[] enemies)
        {
            battleCamera = camera;
            partyAnchors = party;
            enemyAnchors = enemies;
        }

        public BattleUnitView ViewOf(BattleUnit unit) => unit != null && views.TryGetValue(unit, out var v) ? v : null;

        void Start()
        {
            if (!StartedExternally) RunStandalone();
        }

        /// <summary>Play direto na cena de batalha: luta em loop contra o encontro de teste.</summary>
        async void RunStandalone()
        {
            try
            {
                await Awaitable.NextFrameAsync(destroyCancellationToken);
                if (StartedExternally) return;
                var db = GameDatabase.Load();
                while (!StartedExternally)
                {
                    var enc = db.Find<EncounterDefinition>(standaloneEncounterId) ?? db.encounters.FirstOrDefault();
                    var outcome = await Run(new BattleRequest { Encounter = enc }, standalone: true);
                    if (outcome.Result == BattleResult.Defeat) GameSession.Current.RestoreAll();
                    Cleanup();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogException(e); }
        }

        public async Awaitable<BattleOutcome> Run(BattleRequest request, bool standalone = false)
        {
            if (!standalone) StartedExternally = true;
            IsRunning = true;
            try
            {
                LastOutcome = await RunInternal(request, destroyCancellationToken);
                return LastOutcome;
            }
            finally
            {
                IsRunning = false;
            }
        }

        void Cleanup()
        {
            foreach (var view in views.Values) if (view) Destroy(view.gameObject);
            foreach (var marker in markers) if (marker) Destroy(marker.gameObject);
            if (Hud) Destroy(Hud.gameObject);
            views.Clear();
            markers.Clear();
            units.Clear();
        }

        // ======================================================================= fluxo

        async Awaitable<BattleOutcome> RunInternal(BattleRequest request, CancellationToken ct)
        {
            Session = GameSession.Current;
            Rules = new BattleRules(Session.Balance, Session.Rng);
            encounter = request.Encounter;
            loot = new LootDrop();
            stolen.Clear();
            autoAdvance = request.AutoAdvance;
            rounds = damageDealt = damageTaken = knockOuts = 0;
            startedAt = Time.realtimeSinceStartup;
            var commands = request.Commands ?? new PlayerCommandSource();

            SpawnUnits();
            ApplyTheme(request.Theme);
            if (battleCamera != null) cameraHome = battleCamera.transform.position;
            shakeUntil = 0f;
            AudioManager.PlayMusic(encounter.isBoss ? MusicTrack.Boss : MusicTrack.Battle, 0.2f);
            Hud = BattleHud.Create(transform, battleCamera);
            Hud.Bind(Session, units.Where(u => u.Side == Side.Party), units.Where(u => u.Side == Side.Enemies), ViewOf);

            await Hud.Banner(request.FirstStrike ? "Ataque preventivo!" : encounter.displayName, 1.0f, ct);
            if (request.FirstStrike)
            {
                foreach (var enemy in Enemies()) enemy.AddStatus(StatusType.Stun, 1);
                Hud.Refresh();
            }

            BattleResult? result = null;
            while (result == null)
            {
                rounds++;
                var order = TurnOrder.Build(units, Session.Rng);
                for (int i = 0; i < order.Count && result == null; i++)
                {
                    var unit = order[i];
                    if (!unit.IsAlive) continue;
                    Hud.SetTurnOrder(order, i);
                    Hud.SetActive(unit.Side == Side.Party ? unit : null);
                    result = await TakeTurn(unit, commands, ct);
                    Hud.Refresh();
                    result ??= CheckEnd();
                }
            }

            Hud.SetActive(null);
            var outcome = new BattleOutcome { Result = result.Value, Loot = loot };
            var partyUnits = Party().ToList();
            float partyHp = partyUnits.Sum(u => u.Hp) / (float)Mathf.Max(1, partyUnits.Sum(u => u.MaxHp));
            VictorySummary summary = null;
            switch (result.Value)
            {
                case BattleResult.Victory:
                    summary = await Victory(ct);
                    break;
                case BattleResult.Defeat:
                    AudioManager.StopMusic(0.2f);
                    AudioManager.Play(Sfx.Defeat);
                    await Hud.ShowDefeat(ct, !autoAdvance);
                    break;
                case BattleResult.Fled:
                    AudioManager.Play(Sfx.Jump, 0.8f, 1.4f);
                    await FleeSequence(ct);
                    break;
            }

            PlaytestLog.Write(new PlaytestEvent
            {
                type = "batalha",
                encounter = encounter.id,
                result = result.Value.ToString(),
                firstStrike = request.FirstStrike,
                rounds = rounds,
                damageDealt = damageDealt,
                damageTaken = damageTaken,
                knockOuts = knockOuts,
                partyHp = partyHp,
                items = summary?.Items.Count ?? 0,
                itemsRarePlus = summary?.Items.Count(i => i.Rarity >= Rarity.Rare) ?? 0,
                gold = summary?.Gold ?? 0,
                levels = string.Join("/", Session.Party.Select(m => m.Level)),
                seconds = Time.realtimeSinceStartup - startedAt,
            });
            return outcome;
        }

        async Awaitable<BattleResult?> TakeTurn(BattleUnit unit, IBattleCommandSource commands, CancellationToken ct)
        {
            var view = ViewOf(unit);
            var (burn, skip) = Rules.StartTurn(unit);
            if (burn > 0)
            {
                Tally(unit, burn);
                Hud.Popup(view.Top, burn.ToString(), "damage");
                Hud.Popup(view.Top + Vector3.up * 0.4f, "Queimadura", "status");
                await view.HitReaction(ct);
                if (!unit.IsAlive) await OnDeath(unit, ct);
                Hud.Refresh();
                var end = CheckEnd();
                if (end != null) return end;
            }

            if (skip)
            {
                if (unit.IsAlive)
                {
                    Hud.Popup(view.Top, "Atordoado!", "status");
                    await Tween.Delay(0.6f, ct);
                }
                Rules.EndTurn(unit);
                return null;
            }

            BattleCommand command = unit.Side == Side.Party
                ? await commands.Choose(this, unit, ct)
                : EnemyAI.Choose(unit, units, Session.Db.basicAttack, Session.Rng);
            Hud.ShowCommandMenu(null);
            Hud.ShowTargetInfo(null);
            HideMarkers();

            if (command.Type == CommandType.Flee)
            {
                if (CanFlee && Rules.TryFlee()) return BattleResult.Fled;
                await Hud.Banner("Não deu para fugir!", 0.9f, ct);
            }
            else
            {
                await Execute(command, ct);
            }

            Rules.EndTurn(unit);
            return null;
        }

        BattleResult? CheckEnd()
        {
            if (!Enemies().Any(u => u.IsAlive)) return BattleResult.Victory;
            if (!Party().Any(u => u.IsAlive)) return BattleResult.Defeat;
            return null;
        }

        IEnumerable<BattleUnit> Party() => units.Where(u => u.Side == Side.Party);
        IEnumerable<BattleUnit> Enemies() => units.Where(u => u.Side == Side.Enemies);

        // ======================================================================= montagem

        void SpawnUnits()
        {
            Cleanup();
            var balance = Session.Balance;
            var partyCenter = Centroid(partyAnchors);
            var enemyCenter = Centroid(enemyAnchors);

            for (int i = 0; i < Session.Party.Count && i < partyAnchors.Length; i++)
            {
                var unit = new BattleUnit(Session.Party[i], balance.buffPercent) { Slot = i };
                if (unit.Hp <= 0) unit.Hp = 1; // ninguém começa nocauteado
                AddView(unit, partyAnchors[i].position, enemyCenter - partyCenter);
            }

            var counts = new Dictionary<EnemyDefinition, int>();
            foreach (var def in encounter.enemies) counts[def] = counts.TryGetValue(def, out int c) ? c + 1 : 1;
            var seen = new Dictionary<EnemyDefinition, int>();
            for (int i = 0; i < encounter.enemies.Count && i < enemyAnchors.Length; i++)
            {
                var def = encounter.enemies[i];
                seen[def] = seen.TryGetValue(def, out int n) ? n + 1 : 1;
                string name = counts[def] > 1 ? $"{def.displayName} {(char)('A' + seen[def] - 1)}" : def.displayName;
                var unit = new BattleUnit(def, balance.buffPercent, name) { Slot = i };
                AddView(unit, enemyAnchors[i].position, partyCenter - enemyCenter);
            }
        }

        void AddView(BattleUnit unit, Vector3 position, Vector3 facing)
        {
            units.Add(unit);
            views[unit] = BattleUnitView.Create(transform, unit, position, facing);
        }

        void ApplyTheme(BattleTheme? theme)
        {
            if (theme is not BattleTheme t) return;
            var arena = transform.Find("Arena");
            if (arena != null)
            {
                foreach (Transform child in arena)
                {
                    switch (child.name)
                    {
                        case "Grass": Paint(child, t.Ground); break;
                        case "Cliff": Paint(child, t.Cliff); break;
                        case "Water": child.gameObject.SetActive(!t.Indoor); break;
                        case "Torches": child.gameObject.SetActive(t.Indoor); break;
                        case "Tree":
                        case "Decor": child.gameObject.SetActive(!t.Indoor); break;
                        case "Rock": Paint(child, t.Cliff * 1.25f); break;
                    }
                }
            }
            var colors = AtmosphereColors.Underground(t.Sky, t.Sky * 0.35f, t.Ambient);
            var atmosphere = GetComponentInChildren<SceneAtmosphere>(true);
            if (atmosphere != null) atmosphere.SetColors(colors);
            else SceneAtmosphere.ApplyAmbient(colors.ambientSky, colors.ambientGround);
        }

        static void Paint(Transform target, Color color)
        {
            foreach (var r in target.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = Palette.Get(color);
        }

        static Vector3 Centroid(Transform[] anchors)
        {
            if (anchors.Length == 0) return Vector3.zero;
            var sum = Vector3.zero;
            foreach (var a in anchors) sum += a.position;
            return sum / anchors.Length;
        }

        // ======================================================================= alvos

        /// <summary>Seleção de alvo com seta 3D. Devolve null se o jogador cancelar.</summary>
        public async Awaitable<List<BattleUnit>> SelectTargets(BattleUnit actor, TargetType type, CancellationToken ct)
        {
            var candidates = Targeting.Candidates(actor, type, units);
            if (candidates.Count == 0) return null;
            if (type == TargetType.Self) return candidates;

            // Ordena pela posição na tela para ←/→ fazerem sentido.
            if (battleCamera != null)
                candidates = candidates.OrderBy(u => battleCamera.WorldToScreenPoint(ViewOf(u).transform.position).x).ToList();

            bool multi = Targeting.IsMulti(type);
            int index = 0;
            if (!multi && actor.Side == Side.Party && type == TargetType.SingleAlly)
                index = Mathf.Max(0, candidates.IndexOf(candidates.OrderBy(u => u.HpPercent).First()));

            await Awaitable.NextFrameAsync(ct);
            while (true)
            {
                if (multi)
                {
                    ShowMarkers(candidates);
                    Hud.ShowTargetInfo(candidates[0].Side == actor.Side ? "Todos os aliados" : "Todos os inimigos");
                }
                else
                {
                    var nav = GameInput.Nav;
                    int step = nav.x != 0 ? nav.x : -nav.y;
                    if (step != 0)
                    {
                        index = (index + step + candidates.Count) % candidates.Count;
                        AudioManager.Play(Sfx.Cursor);
                    }
                    var target = candidates[index];
                    ShowMarkers(new[] { target });
                    Hud.ShowTargetInfo($"{target.Name}   PV {target.Hp}/{target.MaxHp}");
                }

                if (GameInput.ConfirmDown)
                {
                    AudioManager.Play(Sfx.Confirm);
                    HideMarkers();
                    Hud.ShowTargetInfo(null);
                    return multi ? candidates : new List<BattleUnit> { candidates[index] };
                }
                if (GameInput.CancelDown)
                {
                    AudioManager.Play(Sfx.Cancel);
                    HideMarkers();
                    Hud.ShowTargetInfo(null);
                    return null;
                }
                await Awaitable.NextFrameAsync(ct);
            }
        }

        void ShowMarkers(IReadOnlyList<BattleUnit> targets)
        {
            while (markers.Count < targets.Count)
            {
                var marker = new GameObject("TargetMarker").transform;
                marker.SetParent(transform, false);
                Shapes.Part(PrimitiveType.Cube, marker, Vector3.zero, new Vector3(0.28f, 0.28f, 0.28f), Palette.Gold, new Vector3(45, 0, 45));
                markers.Add(marker);
            }
            for (int i = 0; i < markers.Count; i++)
            {
                bool active = i < targets.Count;
                markers[i].gameObject.SetActive(active);
                if (!active) continue;
                markers[i].position = ViewOf(targets[i]).Top + Vector3.up * (0.35f + Mathf.Sin(Time.time * 6f) * 0.1f);
                markers[i].Rotate(0f, 180f * Time.deltaTime, 0f, Space.World);
            }
        }

        void HideMarkers()
        {
            foreach (var m in markers) if (m) m.gameObject.SetActive(false);
        }

        // ======================================================================= execução

        async Awaitable Execute(BattleCommand command, CancellationToken ct)
        {
            var actor = command.Actor;
            switch (command.Type)
            {
                case CommandType.Defend:
                    AudioManager.Play(Sfx.Block, 0.7f);
                    actor.AddStatus(StatusType.Defending, 1);
                    Hud.Popup(ViewOf(actor).Top, "Defesa!", "status");
                    await ViewOf(actor).Hop(ct, 0.25f, 0.25f);
                    return;

                case CommandType.Item:
                    await UseItem(command, ct);
                    return;
            }

            var ability = command.Type == CommandType.Attack || command.Ability == null ? Session.Db.basicAttack : command.Ability;
            if (actor.Side == Side.Party && ability.energyCost > 0)
            {
                int cost = Rules.EnergyCost(actor, ability);
                if (!Session.SpendEnergy(cost)) ability = Session.Db.basicAttack;
                Hud.Refresh();
            }
            if (ability != Session.Db.basicAttack) Hud.ShowBanner(ability.displayName, 1.1f);

            var targets = ResolveTargets(actor, ability, command.Targets);
            if (targets.Count == 0) return;

            switch (ability.kind)
            {
                case AbilityKind.PhysicalAttack when Targeting.IsMulti(ability.target):
                    await GroupSlam(actor, targets, ability, ct);
                    break;
                case AbilityKind.PhysicalAttack:
                    await Melee(actor, targets[0], ability, ct);
                    break;
                case AbilityKind.MagicAttack:
                    await Spell(actor, targets, ability, ct);
                    break;
                case AbilityKind.Heal:
                    await Heal(actor, targets, ability, ct);
                    break;
                case AbilityKind.Revive:
                    await Revive(actor, targets, ability.power, Palette.Gold, ct);
                    break;
                case AbilityKind.Buff:
                case AbilityKind.Taunt:
                    await Buff(actor, targets, ability, ct);
                    break;
                case AbilityKind.Steal:
                    await Steal(actor, targets[0], ct);
                    break;
            }
        }

        /// <summary>Se o alvo escolhido morreu antes da vez, redireciona para outro válido.</summary>
        List<BattleUnit> ResolveTargets(BattleUnit actor, AbilityDefinition ability, List<BattleUnit> chosen)
        {
            var valid = Targeting.Candidates(actor, ability.target, units);
            if (Targeting.IsMulti(ability.target)) return valid;
            var alive = chosen.Where(valid.Contains).ToList();
            if (alive.Count > 0) return alive;
            return valid.Count > 0 ? new List<BattleUnit> { valid[0] } : new List<BattleUnit>();
        }

        // ---------- janelas de timed hit ----------

        sealed class TimedPress : IDisposable
        {
            readonly IDisposable subscription;
            readonly string kind;
            readonly string abilityId;
            readonly float latency;
            bool disposed;
            public TimedHitWindow Window { get; }
            public BattleUnit Presser { get; }

            public TimedPress(BattleUnit presser, float secondsToImpact, BalanceConfig balance, string kind, string abilityId = null)
            {
                Presser = presser != null && presser.Side == Side.Party ? presser : null;
                this.kind = kind;
                this.abilityId = abilityId;
                latency = balance.timingLatencyCompensation;
                double now = GameInput.Now;
                float scale = Mathf.Max(0.01f, Time.timeScale);
                var (perfect, good) = TimedHitEvaluator.Windows(balance, Presser?.Stat(StatType.TimingWindow) ?? 0f);
                // O impacto aparece na tela um pouco depois do quadro lógico: desloca o centro da janela.
                Window = new TimedHitWindow(now, now + secondsToImpact / scale + latency, perfect, good);
                if (Presser != null) subscription = GameInput.ListenConfirm(Window.RegisterPress);
            }

            public TimedHitResult Result => Presser != null ? Window.Result : TimedHitResult.Miss;

            public async Awaitable WaitClosed(CancellationToken ct)
            {
                while (!Window.IsClosed(GameInput.Now)) await Awaitable.NextFrameAsync(ct);
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                subscription?.Dispose();
                if (Presser == null) return;
                PlaytestLog.Write(new PlaytestEvent
                {
                    type = "timing",
                    kind = kind,
                    actor = Presser.Name,
                    ability = abilityId,
                    result = Window.Result.ToString(),
                    pressed = Window.HasInput,
                    offsetMs = Window.HasInput ? (float)(Window.PressOffset * 1000.0) : 0f,
                    perfectMs = (float)(Window.PerfectWindow * 1000.0),
                    goodMs = (float)(Window.GoodWindow * 1000.0),
                    latencyMs = latency * 1000f,
                });
            }
        }

        /// <summary>Arma a janela de timing e, se for o jogador, mostra o anel sobre <paramref name="ringAt"/>.</summary>
        TimedPress Press(BattleUnit presser, float secondsToImpact, string kind, string abilityId, Vector3 ringAt, bool block)
        {
            var press = new TimedPress(presser, secondsToImpact, Session.Balance, kind, abilityId);
            if (press.Presser != null && GameSettings.TimingRing)
                Hud.ShowTimingRing(press.Window, ringAt, press.Window.ImpactAt - Session.Balance.timingLatencyCompensation, block);
            return press;
        }

        void ShowTiming(TimedPress press, Vector3 at, bool isBlock)
        {
            if (press.Presser == null) return;
            var r = press.Result;
            if (r != TimedHitResult.Miss)
            {
                string text = isBlock ? (r == TimedHitResult.Perfect ? "DEFESA PERFEITA!" : "DEFESA!") : TimedHitEvaluator.Label(r);
                if (TimingFeedback.ShowMilliseconds) text += $" ({TimingFeedback.Milliseconds(press.Window.PressOffset)})";
                Hud.Popup(at + Vector3.up * 0.6f, text, "timing");
                if (r == TimedHitResult.Perfect) PerfectJuice(at, isBlock);
            }
            else if (press.Window.HasInput)
            {
                string text = press.Window.PressOffset < 0 ? "Cedo!" : "Tarde!";
                if (TimingFeedback.ShowMilliseconds) text += $" ({TimingFeedback.Milliseconds(press.Window.PressOffset)})";
                Hud.Popup(at + Vector3.up * 0.6f, text, "status", 0.8f);
                AudioManager.Play(Sfx.Miss, 0.7f);
            }
        }

        /// <summary>Golpe/defesa perfeitos: congela por um instante, solta estrelas e treme a câmera.</summary>
        void PerfectJuice(Vector3 at, bool isBlock)
        {
            HitStop.Freeze(isBlock ? 0.06f : 0.09f);
            var color = isBlock ? new Color(0.55f, 0.85f, 1f) : Palette.Gold;
            _ = Shapes.Burst(transform, at - Vector3.up * 0.2f, color, 8, 1.1f, 0.18f, 0.4f, 0.5f, destroyCancellationToken, glowing: true);
            Shake(isBlock ? 0.05f : 0.1f, 0.2f);
            Fx.Stars(at, color, 12);
        }

        void Shake(float magnitude, float duration)
        {
            if (battleCamera == null) return;
            float now = Time.unscaledTime;
            float remaining = shakeUntil > now ? shakeMagnitude * (shakeUntil - now) / Mathf.Max(0.01f, shakeDuration) : 0f;
            shakeMagnitude = Mathf.Max(magnitude, remaining);
            shakeDuration = duration;
            shakeUntil = now + duration;
        }

        void LateUpdate()
        {
            if (battleCamera == null || !IsRunning) return;
            float remaining = shakeUntil - Time.unscaledTime;
            battleCamera.transform.position = remaining > 0f
                ? cameraHome + UnityEngine.Random.insideUnitSphere * (shakeMagnitude * remaining / Mathf.Max(0.01f, shakeDuration))
                : cameraHome;
        }

        static Sfx HitSound(HitOutcome outcome)
        {
            if (outcome.Block == TimedHitResult.Perfect) return Sfx.BlockPerfect;
            if (outcome.Block == TimedHitResult.Good) return Sfx.Block;
            if (outcome.Timing == TimedHitResult.Perfect) return Sfx.Perfect;
            if (outcome.Timing == TimedHitResult.Good) return Sfx.HitGood;
            return Sfx.Hit;
        }

        void ShowOutcome(HitOutcome outcome)
        {
            var view = ViewOf(outcome.Target);
            if (view == null) return;
            if (outcome.Damage > 0) Tally(outcome.Target, outcome.Damage);
            if (outcome.Damage > 0)
            {
                Hud.Popup(view.Top, outcome.Crit ? $"{outcome.Damage}!" : outcome.Damage.ToString(), outcome.Crit ? "crit" : "damage");
                _ = view.HitReaction(destroyCancellationToken);
                Fx.Sparks(view.Center, new Color(1f, 0.92f, 0.65f));
                if (outcome.Crit) Fx.Stars(view.Center, new Color(1f, 0.55f, 0.3f));
                AudioManager.Play(HitSound(outcome), outcome.Crit ? 1f : 0.85f, outcome.Crit ? 0.85f : 1f);
                if (outcome.Crit) Shake(0.16f, 0.3f);
                else if (outcome.Target.Side == Side.Party && encounter != null && encounter.isBoss) Shake(0.1f, 0.25f);
            }
            if (outcome.Healed > 0)
            {
                Hud.Popup(view.Top, $"+{outcome.Healed}", "heal");
                Fx.Rising(view.transform.position, new Color(0.55f, 1f, 0.6f), 0.4f, 12, 0.8f);
            }
            if (outcome.StatusApplied && outcome.Damage <= 0) AudioManager.Play(Sfx.Buff, 0.7f);
            if (outcome.StatusApplied) Hud.Popup(view.Top + Vector3.up * 0.45f, StatusText.Name(outcome.Status), "status");
            Hud.Refresh();
        }

        // ---------- ações ----------

        async Awaitable Melee(BattleUnit actor, BattleUnit target, AbilityDefinition ability, CancellationToken ct)
        {
            var av = ViewOf(actor);
            var tv = ViewOf(target);
            bool party = actor.Side == Side.Party;

            var holdResult = TimedHitResult.Miss;
            if (party && ability.timing == TimedHitType.HoldRelease) holdResult = await Charge(actor, ct);

            Vector3 toTarget = tv.transform.position - av.Home;
            toTarget.y = 0f;
            Vector3 dir = toTarget.normalized;
            Vector3 strikePos = tv.transform.position - dir * (tv.Radius + av.Radius + 0.15f);
            strikePos.y = av.Home.y;

            av.Face(dir);
            await Tween.Arc(av.transform, strikePos, 0.45f, 0.32f, ct);

            int hits = Mathf.Max(1, ability.hits);
            for (int h = 0; h < hits && target.IsAlive && actor.IsAlive; h++)
            {
                float windup = h == 0 ? 0.42f : 0.3f;
                var presser = party
                    ? (ability.timing is TimedHitType.SinglePress or TimedHitType.MultiPress ? actor : null)
                    : target;
                TimedHitResult pressResult;
                using (var press = Press(presser, windup, party ? (hits > 1 ? "multi" : "ataque") : "defesa", ability.id, tv.Center, block: !party))
                {
                    await av.WindUp(windup, ct);
                    _ = av.Strike(ct);
                    await press.WaitClosed(ct);
                    pressResult = press.Result;
                    ShowTiming(press, tv.Top, isBlock: !party);
                }

                var timing = party ? (ability.timing == TimedHitType.HoldRelease ? holdResult : pressResult) : TimedHitResult.Miss;
                var block = party ? TimedHitResult.Miss : pressResult;
                var outcome = Rules.ResolveAttack(actor, target, ability, timing, block);
                ShowOutcome(outcome);
                await AfterHit(actor, outcome, ct);
                await Tween.Delay(0.12f, ct);
            }

            if (actor.IsAlive)
            {
                await Tween.Arc(av.transform, av.Home, 0.45f, 0.32f, ct);
                av.FaceHome();
            }
        }

        async Awaitable AfterHit(BattleUnit actor, HitOutcome outcome, CancellationToken ct)
        {
            if (outcome.Reflected > 0)
            {
                Tally(actor, outcome.Reflected);
                Hud.Popup(ViewOf(actor).Top, $"{outcome.Reflected} (reflexo)", "damage");
            }
            if (outcome.LifeStolen > 0)
                Hud.Popup(ViewOf(actor).Top, $"+{outcome.LifeStolen}", "heal");
            if (outcome.Killed) await OnDeath(outcome.Target, ct);
            if (outcome.Reflected > 0 && !actor.IsAlive) await OnDeath(actor, ct);
            Hud.Refresh();
        }

        /// <summary>Segurar Confirmar até a barra encher e soltar no ponto.</summary>
        async Awaitable<TimedHitResult> Charge(BattleUnit actor, CancellationToken ct)
        {
            var view = ViewOf(actor);
            var (perfect, good) = TimedHitEvaluator.Windows(Session.Balance, actor.Stat(StatType.TimingWindow));
            float latency = Session.Balance.timingLatencyCompensation;
            var window = new HoldReleaseWindow(GameInput.Now, 0.9 + latency, perfect, good);
            Hud.ShowCharge(true);
            Hud.ShowBanner("Segure Confirmar...", 0f);
            double giveUpAt = GameInput.Now + 3.0;
            bool fullPlayed = false;
            using (GameInput.ListenConfirm(window.RegisterPress, window.RegisterRelease))
            {
                while (!window.IsDone)
                {
                    double now = GameInput.Now;
                    window.Update(now);
                    if (!window.PressedAt.HasValue && now > giveUpAt) window.Cancel();
                    float charge = window.Charge01(now);
                    Hud.SetCharge(charge);
                    if (charge >= 1f && !fullPlayed)
                    {
                        fullPlayed = true;
                        AudioManager.Play(Sfx.ChargeFull);
                    }
                    if (view.Visual) view.Visual.localPosition = new Vector3(Mathf.Sin(Time.time * 60f) * 0.04f * charge, view.Visual.localPosition.y, 0f);
                    await Awaitable.NextFrameAsync(ct);
                }
            }
            Hud.ShowCharge(false);
            Hud.ShowBanner("", 0f);
            if (view.Visual) view.Visual.localPosition = new Vector3(0f, view.Visual.localPosition.y, 0f);

            double releaseOffset = window.ReleasedAt.HasValue ? window.ReleasedAt.Value - window.TargetAt.Value : 0;
            PlaytestLog.Write(new PlaytestEvent
            {
                type = "timing",
                kind = "carga",
                actor = actor.Name,
                ability = "investida",
                result = window.Result.ToString(),
                pressed = window.ReleasedAt.HasValue,
                offsetMs = (float)(releaseOffset * 1000.0),
                perfectMs = (float)(perfect * 1000.0),
                goodMs = (float)(good * 1000.0),
                latencyMs = latency * 1000f,
            });
            string ms = TimingFeedback.ShowMilliseconds && window.ReleasedAt.HasValue ? $" ({TimingFeedback.Milliseconds(releaseOffset)})" : "";
            if (window.Result != TimedHitResult.Miss)
            {
                Hud.Popup(view.Top + Vector3.up * 0.6f, TimedHitEvaluator.Label(window.Result) + ms, "timing");
                if (window.Result == TimedHitResult.Perfect) AudioManager.Play(Sfx.ChargeFull, 1f, 1.2f);
            }
            else if (window.PressedAt.HasValue) Hud.Popup(view.Top + Vector3.up * 0.6f, "Carga falhou" + ms, "status", 0.8f);
            return window.Result;
        }

        /// <summary>Ataque físico em área: pula e cai no meio dos alvos (um único bloqueio vale para todos).</summary>
        async Awaitable GroupSlam(BattleUnit actor, List<BattleUnit> targets, AbilityDefinition ability, CancellationToken ct)
        {
            var av = ViewOf(actor);
            bool party = actor.Side == Side.Party;
            var center = targets.Select(t => ViewOf(t).transform.position).Aggregate(Vector3.zero, (a, b) => a + b) / targets.Count;
            var landing = Vector3.Lerp(av.Home, center, 0.45f);

            const float airTime = 0.6f;
            var presser = party ? actor : targets.FirstOrDefault(t => t.Side == Side.Party);
            TimedHitResult result;
            var ringAt = party || presser == null ? center + Vector3.up * 0.8f : ViewOf(presser).Center;
            using (var press = Press(presser, airTime, party ? "area" : "defesa", ability.id, ringAt, block: !party))
            {
                await Tween.Arc(av.transform, landing, 2.2f, airTime, ct);
                Shake(0.12f, 0.25f);
                AudioManager.Play(Sfx.Land, 1f, 0.7f);
                foreach (var t in targets) _ = Tween.Shake(ViewOf(t).Visual, 0.3f, 0.15f, ct);
                await press.WaitClosed(ct);
                result = press.Result;
                ShowTiming(press, center, isBlock: !party);
            }

            foreach (var target in targets)
            {
                var outcome = Rules.ResolveAttack(actor, target, ability, party ? result : TimedHitResult.Miss, party ? TimedHitResult.Miss : result);
                ShowOutcome(outcome);
                await AfterHit(actor, outcome, ct);
            }
            if (actor.IsAlive) await Tween.Arc(av.transform, av.Home, 0.6f, 0.35f, ct);
        }

        static Color SpellColor(AbilityDefinition ability) => ability.id switch
        {
            "faisca" or "e_bafo" => new Color(1f, 0.45f, 0.1f),
            "nevasca" => new Color(0.6f, 0.9f, 1f),
            "trovao" => new Color(1f, 0.95f, 0.3f),
            _ => new Color(0.75f, 0.45f, 1f),
        };

        async Awaitable Spell(BattleUnit actor, List<BattleUnit> targets, AbilityDefinition ability, CancellationToken ct)
        {
            var av = ViewOf(actor);
            bool party = actor.Side == Side.Party;
            await av.Hop(ct, 0.4f, 0.25f);

            const float travel = 0.55f;
            var presser = party ? actor : targets.FirstOrDefault(t => t.Side == Side.Party);
            var color = SpellColor(ability);
            AudioManager.Play(Sfx.Magic, 0.8f, party ? 1f : 0.8f);
            TimedHitResult result;
            var ringAt = party || presser == null ? ViewOf(targets[0]).Center : ViewOf(presser).Center;
            using (var press = Press(presser, travel, party ? "magia" : "defesa", ability.id, ringAt, block: !party))
            {
                var flights = new List<Awaitable>();
                foreach (var t in targets)
                {
                    var tv = ViewOf(t);
                    var from = targets.Count > 1 ? tv.Center + Vector3.up * 4f : av.Center + Vector3.up * 0.3f;
                    flights.Add(Projectile(from, tv.Center, color, travel, ct));
                }
                foreach (var f in flights) await f;
                await press.WaitClosed(ct);
                result = press.Result;
                ShowTiming(press, ViewOf(targets[0]).Top, isBlock: !party);
            }

            foreach (var target in targets)
            {
                var outcome = Rules.ResolveAttack(actor, target, ability, party ? result : TimedHitResult.Miss, party ? TimedHitResult.Miss : result);
                ShowOutcome(outcome);
                await AfterHit(actor, outcome, ct);
            }
        }

        async Awaitable Projectile(Vector3 from, Vector3 to, Color color, float duration, CancellationToken ct)
        {
            var orb = Shapes.Part(MeshLibrary.Icosphere(1), transform, Vector3.zero, Vector3.one * 0.35f, Palette.Emissive(color, 2.2f)).transform;
            orb.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Shapes.GlowQuad(orb, Vector3.zero, 2.4f, Palette.Glow(new Color(color.r, color.g, color.b, 0.6f))).AddComponent<Billboard>();
            Fx.Trail(orb.gameObject, color, 0.3f, 0.3f);
            orb.position = from;
            try
            {
                await Tween.Run(duration, k =>
                {
                    if (!orb) return;
                    orb.position = Vector3.Lerp(from, to, Tween.EaseInQuad(k));
                    orb.localScale = Vector3.one * (0.3f + 0.15f * Mathf.Sin(k * 20f));
                }, ct);
                if (orb)
                {
                    Fx.Burst(to, color, 16, 3.5f, 0.2f, 0.45f, gravity: 0.2f, radius: 0.2f);
                    await Tween.ScaleTo(orb, Vector3.one * 1.1f, 0.08f, ct);
                }
            }
            finally
            {
                if (orb) Destroy(orb.gameObject);
            }
        }

        async Awaitable Sparkles(BattleUnitView target, Color color, float duration, CancellationToken ct)
        {
            Fx.Rising(target.transform.position, color, 0.5f, 22, duration + 0.4f);
            await Tween.Delay(duration, ct);
        }

        async Awaitable Heal(BattleUnit actor, List<BattleUnit> targets, AbilityDefinition ability, CancellationToken ct)
        {
            await ViewOf(actor).Hop(ct, 0.4f, 0.25f);
            const float duration = 0.5f;
            AudioManager.Play(Sfx.Heal);
            TimedHitResult result;
            using (var press = Press(actor, duration, "cura", ability.id, ViewOf(targets[0]).Center, block: false))
            {
                var fx = targets.Select(t => Sparkles(ViewOf(t), new Color(0.5f, 1f, 0.55f), duration, ct)).ToList();
                foreach (var f in fx) await f;
                await press.WaitClosed(ct);
                result = press.Result;
                ShowTiming(press, ViewOf(targets[0]).Top, false);
            }
            foreach (var t in targets) ShowOutcome(Rules.ResolveHeal(actor, t, ability, result));
        }

        async Awaitable Revive(BattleUnit actor, List<BattleUnit> targets, float hpPercent, Color color, CancellationToken ct)
        {
            await ViewOf(actor).Hop(ct, 0.4f, 0.25f);
            foreach (var t in targets)
            {
                AudioManager.Play(Sfx.Heal, 1f, 1.15f);
                await Sparkles(ViewOf(t), color, 0.5f, ct);
                var outcome = Rules.Revive(t, hpPercent);
                if (outcome.Revived) ViewOf(t).Revive();
                ShowOutcome(outcome);
            }
        }

        async Awaitable Buff(BattleUnit actor, List<BattleUnit> targets, AbilityDefinition ability, CancellationToken ct)
        {
            await ViewOf(actor).Hop(ct, 0.5f, 0.3f);
            AudioManager.Play(Sfx.Buff);
            foreach (var t in targets)
            {
                _ = Tween.Squash(ViewOf(t).Visual, -0.25f, 0.3f, ct);
                if (ability.appliesStatus) ShowOutcome(Rules.ApplyStatus(t, ability.status, ability.statusTurns));
            }
            await Tween.Delay(0.35f, ct);
        }

        async Awaitable Steal(BattleUnit actor, BattleUnit target, CancellationToken ct)
        {
            var av = ViewOf(actor);
            var tv = ViewOf(target);
            Vector3 dir = (tv.transform.position - av.Home).normalized;
            Vector3 touch = tv.transform.position - dir * (tv.Radius + 0.3f);
            touch.y = av.Home.y;

            TimedHitResult result;
            using (var press = Press(actor, 0.35f, "roubo", "roubar", tv.Center, block: false))
            {
                av.Face(dir);
                await Tween.MoveTo(av.transform, touch, 0.35f, ct, Tween.EaseInQuad);
                _ = tv.HitReaction(ct);
                await press.WaitClosed(ct);
                result = press.Result;
                ShowTiming(press, tv.Top, false);
            }

            switch (BattleEffects.Steal(Session, target, result, out var item))
            {
                case StealResult.Stolen:
                    stolen.Add(item);
                    Hud.PopupRarity(tv.Top, item);
                    AudioManager.Play(Sfx.Steal);
                    if (item.Rarity >= Rarity.Rare) AudioManager.PlayLoot(item.Rarity);
                    break;
                case StealResult.InventoryFull:
                    Hud.Popup(tv.Top, "Mochila cheia!", "status");
                    break;
                default:
                    Hud.Popup(tv.Top, "Nada para roubar!", "status");
                    AudioManager.Play(Sfx.Miss, 0.7f);
                    break;
            }

            await Tween.Arc(av.transform, av.Home, 0.4f, 0.3f, ct);
            av.FaceHome();
        }

        async Awaitable UseItem(BattleCommand command, CancellationToken ct)
        {
            var item = command.Item;
            var actor = command.Actor;
            if (item == null || !Session.Inventory.ConsumeOne(item)) return;
            Hud.ShowBanner(item.displayName, 1f);
            await ViewOf(actor).Hop(ct, 0.4f, 0.25f);
            AudioManager.Play(item.effect == ConsumableEffect.RestoreEnergy ? Sfx.Buff : Sfx.Heal);

            if (item.effect == ConsumableEffect.RestoreEnergy)
            {
                var outcome = BattleEffects.ApplyItem(Session, Rules, item, actor);
                Hud.Popup(ViewOf(actor).Top, $"+{outcome.EnergyRestored} PE", "heal");
            }
            else
            {
                foreach (var t in ResolveTargetsForItem(command))
                {
                    await Sparkles(ViewOf(t), item.color, 0.4f, ct);
                    var outcome = BattleEffects.ApplyItem(Session, Rules, item, t);
                    if (outcome.Revived) ViewOf(t).Revive();
                    ShowOutcome(outcome);
                }
            }
            Hud.Refresh();
        }

        List<BattleUnit> ResolveTargetsForItem(BattleCommand command)
        {
            var valid = Targeting.Candidates(command.Actor, command.Item.target, units);
            var chosen = command.Targets.Where(valid.Contains).ToList();
            return chosen.Count > 0 ? chosen : valid.Take(1).ToList();
        }

        // ======================================================================= mortes e fim

        async Awaitable OnDeath(BattleUnit unit, CancellationToken ct)
        {
            var view = ViewOf(unit);
            if (unit.Side == Side.Party)
            {
                knockOuts++;
                AudioManager.Play(Sfx.PartyKnockOut);
                Shake(0.12f, 0.3f);
            }
            if (unit.Side == Side.Enemies)
            {
                AudioManager.Play(Sfx.EnemyDie);
                Fx.Poof(view.Center, new Color(0.95f, 0.93f, 1f, 0.85f));
                Fx.Stars(view.Center, Palette.Gold, 6);
                var drop = BattleEffects.RollEnemyLoot(Session, unit);
                loot.Merge(drop);
                if (drop.BestRarity is Rarity best)
                {
                    _ = Shapes.LootBeam(transform, view.transform.position, RarityInfo.Color(best), ct);
                    if (best >= Rarity.Rare) AudioManager.PlayLoot(best);
                }
            }
            await view.Die(ct);
        }

        async Awaitable<VictorySummary> Victory(CancellationToken ct)
        {
            await Tween.Delay(0.4f, ct);
            AudioManager.StopMusic(0.15f);
            AudioManager.Play(Sfx.Victory);
            foreach (var hero in Party().Where(u => u.IsAlive))
            {
                _ = ViewOf(hero).Hop(ct, 0.7f, 0.4f);
                Fx.Rising(ViewOf(hero).transform.position, Palette.Gold, 0.5f, 16, 1.1f);
            }

            var summary = BattleEffects.GrantVictory(Session, encounter, loot, Party(), stolen);
            foreach (var hero in Party()) hero.SyncMaxHp();
            Hud.Refresh();
            if (summary.LevelUps > 0) _ = PlayLater(Sfx.LevelUp, 1.9f, ct);

            await Hud.ShowVictory(summary, ct, !autoAdvance);
            return summary;
        }

        static async Awaitable PlayLater(Sfx sfx, float seconds, CancellationToken ct)
        {
            try { await Awaitable.WaitForSecondsAsync(seconds, ct); }
            catch (OperationCanceledException) { return; }
            AudioManager.Play(sfx);
        }

        async Awaitable FleeSequence(CancellationToken ct)
        {
            Hud.ShowBanner("Fugiu!", 1f);
            var runs = new List<Awaitable>();
            foreach (var hero in Party().Where(u => u.IsAlive))
            {
                var view = ViewOf(hero);
                var away = view.Home - view.HomeFacing.normalized * 6f;
                view.Face(-view.HomeFacing);
                runs.Add(Tween.MoveTo(view.transform, away, 0.6f, ct, Tween.EaseInQuad));
            }
            foreach (var r in runs) await r;
        }
    }
}
