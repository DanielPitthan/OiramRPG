using System;
using System.Collections.Generic;
using System.Linq;
using Oiram.Core;
using Oiram.Loot;
using Oiram.Stats;

namespace Oiram.Characters
{
    /// <summary>Progresso de um personagem num job (estilo FFT): JP acumulado, JP disponível e habilidades aprendidas.</summary>
    public sealed class JobProgress
    {
        readonly HashSet<AbilityDefinition> learned = new();

        public JobDefinition Job { get; }
        public int TotalJp { get; private set; }
        public int AvailableJp { get; private set; }

        public JobProgress(JobDefinition job) => Job = job;

        public void AddJp(int amount)
        {
            if (amount <= 0) return;
            TotalJp += amount;
            AvailableJp += amount;
        }

        public bool IsLearned(AbilityDefinition ability) => ability != null && learned.Contains(ability);

        public bool CanLearn(AbilityDefinition ability) =>
            ability != null && Job.abilities.Contains(ability) && !learned.Contains(ability) && AvailableJp >= ability.jpCost;

        public bool TryLearn(AbilityDefinition ability)
        {
            if (!CanLearn(ability)) return false;
            AvailableJp -= ability.jpCost;
            learned.Add(ability);
            return true;
        }

        public void GrantFree(AbilityDefinition ability)
        {
            if (ability != null) learned.Add(ability);
        }

        /// <summary>Aprendidas, na ordem em que o job as lista.</summary>
        public IEnumerable<AbilityDefinition> LearnedInOrder() => Job.abilities.Where(learned.Contains);

        public IEnumerable<AbilityDefinition> Learned => learned;

        /// <summary>Carregar jogo salvo.</summary>
        public void Restore(int totalJp, int availableJp, IEnumerable<AbilityDefinition> abilities)
        {
            TotalJp = Math.Max(0, totalJp);
            AvailableJp = Math.Max(0, availableJp);
            learned.Clear();
            foreach (var a in abilities) if (a != null) learned.Add(a);
        }
    }

    /// <summary>Estado em jogo de um membro da party.</summary>
    public sealed class PartyMember
    {
        readonly Dictionary<JobDefinition, JobProgress> progress = new();
        readonly Dictionary<EquipSlot, ItemInstance> equipment = new();
        readonly BalanceConfig balance;

        public CharacterDefinition Definition { get; }
        public string Name => Definition.displayName;
        public int Level { get; private set; }
        public int Xp { get; private set; }
        public int CurrentHp { get; set; }
        public JobDefinition Job { get; private set; }
        public JobDefinition SecondaryJob { get; private set; }
        public AbilityDefinition SupportPassive { get; private set; }

        public PartyMember(CharacterDefinition definition, BalanceConfig balance, int level = 1)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.balance = balance ?? throw new ArgumentNullException(nameof(balance));
            Level = Math.Max(1, level);
            Job = definition.startingJob;
            foreach (var ability in definition.startingAbilities)
                ProgressFor(FindJobOf(ability) ?? Job).GrantFree(ability);
            CurrentHp = MaxHp;
        }

        JobDefinition FindJobOf(AbilityDefinition ability)
        {
            if (Job != null && Job.abilities.Contains(ability)) return Job;
            foreach (var p in progress.Keys)
                if (p.abilities.Contains(ability)) return p;
            return null;
        }

        // ---------- Jobs ----------

        public JobProgress ProgressFor(JobDefinition job)
        {
            if (job == null) return null;
            if (!progress.TryGetValue(job, out var p))
            {
                p = new JobProgress(job);
                progress[job] = p;
            }
            return p;
        }

        public int JobLevel(JobDefinition job) => job == null ? 0 : balance.JobLevelForJp(ProgressFor(job).TotalJp);

        public bool IsJobUnlocked(JobDefinition job)
        {
            if (job == null) return false;
            if (job == Definition.startingJob || progress.ContainsKey(job) && ProgressFor(job).TotalJp > 0) return true;
            foreach (var req in job.requirements)
                if (req.job != null && JobLevel(req.job) < req.level) return false;
            return true;
        }

        /// <summary>Troca o job principal. Itens que o novo job não pode usar voltam para <paramref name="unequipped"/>.</summary>
        public bool SetJob(JobDefinition job, List<ItemInstance> unequipped = null)
        {
            if (job == null || !IsJobUnlocked(job)) return false;
            Job = job;
            if (SecondaryJob == job) SecondaryJob = null;

            foreach (var slot in equipment.Keys.ToList())
            {
                var item = equipment[slot];
                if (!job.CanEquip(item.Category))
                {
                    equipment.Remove(slot);
                    unequipped?.Add(item);
                }
            }
            ClampHp();
            return true;
        }

        public bool SetSecondaryJob(JobDefinition job)
        {
            if (job == null) { SecondaryJob = null; return true; }
            if (job == Job || !IsJobUnlocked(job)) return false;
            SecondaryJob = job;
            return true;
        }

        public bool SetSupportPassive(AbilityDefinition passive)
        {
            if (passive == null) { SupportPassive = null; ClampHp(); return true; }
            if (!passive.IsPassive || !LearnedPassives().Contains(passive)) return false;
            SupportPassive = passive;
            ClampHp();
            return true;
        }

        public IEnumerable<AbilityDefinition> PrimaryAbilities() =>
            ProgressFor(Job).LearnedInOrder().Where(a => !a.IsPassive);

        public IEnumerable<AbilityDefinition> SecondaryAbilities() =>
            SecondaryJob == null ? Enumerable.Empty<AbilityDefinition>() : ProgressFor(SecondaryJob).LearnedInOrder().Where(a => !a.IsPassive);

        public IEnumerable<AbilityDefinition> LearnedPassives() =>
            progress.Values.SelectMany(p => p.LearnedInOrder()).Where(a => a.IsPassive);

        /// <summary>JP vai para o job atual (com bônus de JP de equipamentos/passivas). Retorna o JP efetivo.</summary>
        public int GainJp(int amount)
        {
            if (amount <= 0) return 0;
            int total = (int)Math.Round(amount * (1f + ComputeStats()[StatType.JPBonus] / 100f));
            ProgressFor(Job).AddJp(total);
            return total;
        }

        // ---------- Nível e atributos ----------

        public int XpToNext => balance.XpToNextLevel(Level);

        /// <summary>Adiciona XP e devolve quantos níveis subiu. Subir de nível cura totalmente.</summary>
        public int GainXp(int amount)
        {
            if (amount <= 0 || Level >= balance.maxCharacterLevel) return 0;
            Xp += amount;
            int gained = 0;
            while (Level < balance.maxCharacterLevel && Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                gained++;
            }
            if (gained > 0) CurrentHp = MaxHp;
            return gained;
        }

        /// <summary>(base + crescimento × (nível − 1)) × multiplicador do job.</summary>
        public StatBlock BaseStats()
        {
            var b = Definition.baseStats;
            var g = Definition.growthPerLevel;
            var m = Job != null ? Job.statMultipliers : PrimaryStats.One;
            int n = Level - 1;
            return new PrimaryStats(
                (b.hp + g.hp * n) * m.hp,
                (b.attack + g.attack * n) * m.attack,
                (b.defense + g.defense * n) * m.defense,
                (b.magic + g.magic * n) * m.magic,
                (b.resistance + g.resistance * n) * m.resistance,
                (b.speed + g.speed * n) * m.speed).ToBlock();
        }

        public IEnumerable<StatModifier> AllModifiers()
        {
            foreach (var item in equipment.Values)
                foreach (var mod in item.AllModifiers())
                    yield return mod;
            if (SupportPassive != null)
                foreach (var mod in SupportPassive.passiveModifiers)
                    yield return mod;
        }

        public StatBlock ComputeStats() => StatBlock.Compose(BaseStats(), AllModifiers());

        /// <summary>Atributos como ficariam com <paramref name="item"/> no lugar do que está no mesmo slot.</summary>
        public StatBlock PreviewWith(ItemInstance item)
        {
            var mods = new List<StatModifier>();
            foreach (var pair in equipment)
                if (item == null || pair.Key != item.Slot)
                    mods.AddRange(pair.Value.AllModifiers());
            if (item != null) mods.AddRange(item.AllModifiers());
            if (SupportPassive != null) mods.AddRange(SupportPassive.passiveModifiers);
            return StatBlock.Compose(BaseStats(), mods);
        }

        public int MaxHp => Math.Max(1, (int)Math.Round(ComputeStats()[StatType.MaxHP]));
        public bool IsAlive => CurrentHp > 0;

        public void ClampHp() => CurrentHp = Math.Max(0, Math.Min(CurrentHp, MaxHp));
        public void FullHeal() => CurrentHp = MaxHp;

        // ---------- Equipamento ----------

        public ItemInstance GetEquipped(EquipSlot slot) => equipment.TryGetValue(slot, out var item) ? item : null;
        public IEnumerable<ItemInstance> EquippedItems() => equipment.Values;

        public bool CanEquip(ItemInstance item) => item != null && Job != null && Job.CanEquip(item.Category);

        /// <summary>Equipa e devolve o item que estava no slot (ou null). Lança se o job não permitir.</summary>
        public ItemInstance Equip(ItemInstance item)
        {
            if (!CanEquip(item))
                throw new InvalidOperationException($"{Job?.displayName} não pode equipar {item?.Name}.");
            var previous = GetEquipped(item.Slot);
            equipment[item.Slot] = item;
            ClampHp();
            return previous;
        }

        // ---------- Carregar jogo salvo ----------

        public IEnumerable<JobProgress> AllProgress() => progress.Values;

        public void RestoreState(int level, int xp, int hp)
        {
            Level = Math.Max(1, level);
            Xp = Math.Max(0, xp);
            CurrentHp = hp;
            ClampHp();
        }

        public void RestoreJobs(JobDefinition job, JobDefinition secondary, AbilityDefinition support)
        {
            if (job != null) Job = job;
            SecondaryJob = secondary != job ? secondary : null;
            SupportPassive = support;
        }

        public void ClearEquipment() => equipment.Clear();

        /// <summary>Equipa sem checar o job (o save já foi válido quando foi gravado).</summary>
        public void RestoreEquipped(ItemInstance item)
        {
            if (item != null) equipment[item.Slot] = item;
        }

        public ItemInstance Unequip(EquipSlot slot)
        {
            if (!equipment.TryGetValue(slot, out var item)) return null;
            equipment.Remove(slot);
            ClampHp();
            return item;
        }
    }
}
