using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Mobs;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// Inspector d'un mob, sur le modèle de l'éditeur "Living entity" de MCreator : apparence,
/// attributs, IA (liste ordonnée de comportements), apparition, sons et butin.
/// </summary>
public sealed class MobAssetViewModel : TexturedAssetViewModel
{
    private readonly MobAsset _mob;
    private MobGoalKind _newGoalKind = MobGoalKind.RandomStroll;
    private string _eggPrimaryText;
    private string _eggSecondaryText;

    public MobAssetViewModel(MobAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel, "Aucune (texture de la créature d'origine)")
    {
        _mob = model;
        _eggPrimaryText = ColorText(model.EggPrimaryColor);
        _eggSecondaryText = ColorText(model.EggSecondaryColor);

        BreedingItem = new ReferencePickerViewModel(
            context, ReferenceDomain.Item, () => _mob.BreedingItem,
            v => Change("Modifier l'item de reproduction", nameof(MobAsset.BreedingItem), () => _mob.BreedingItem, r => _mob.BreedingItem = r, v));
        AmbientSound = SoundPicker(nameof(MobAsset.AmbientSound), () => _mob.AmbientSound, v => _mob.AmbientSound = v);
        HurtSound = SoundPicker(nameof(MobAsset.HurtSound), () => _mob.HurtSound, v => _mob.HurtSound = v);
        DeathSound = SoundPicker(nameof(MobAsset.DeathSound), () => _mob.DeathSound, v => _mob.DeathSound = v);

        BiomeOptions =
        [
            .. GameDataChoices.SpawnBiomes.Select(c => new ToggleOptionViewModel(
                c.Label, () => _mob.SpawnBiomes.Contains(c.Value), included => SetBiome(c.Value, included))),
        ];

        AddGoalCommand = new RelayCommand(AddGoal);
        AddDropCommand = new RelayCommand(AddDrop);
        RebuildGoals();
        RebuildDrops();
    }

    // ----- Listes de choix -----

    public IReadOnlyList<Choice<MobModel>> ModelChoices => GameDataChoices.MobModels;

    public IReadOnlyList<Choice<MobKind>> KindChoices => GameDataChoices.MobKinds;

    public IReadOnlyList<GoalKindOption> GoalKindChoices { get; } =
    [
        .. MobGoalCatalog.All.Select(i => new GoalKindOption(i.Kind, i.RequiresAnimal ? i.Label + " (Animal)" : i.Label)),
    ];

    public double MaxHealth => MobAsset.MaxHealth;

    public double MaxArmor => MobAsset.MaxArmor;

    public double MaxMovementSpeed => MobAsset.MaxMovementSpeed;

    public double MaxAttackDamage => MobAsset.MaxAttackDamage;

    public double MaxFollowRange => MobAsset.MaxFollowRange;

    public int MaxExperience => MobAsset.MaxExperience;

    public float MaxHitbox => MobAsset.MaxHitbox;

    public int MaxSpawnWeight => MobAsset.MaxSpawnWeight;

    public int MaxGroupSize => MobAsset.MaxGroupSize;

    // ----- Apparence -----

    /// <summary>Modèle 3D (nommé ainsi pour ne pas masquer <see cref="AssetViewModel.Model"/>).</summary>
    public MobModel MobModel
    {
        get => _mob.Model;
        set => ChangeIfDifferent("Modifier le modèle", nameof(MobAsset.Model), () => _mob.Model, v => _mob.Model = v, value);
    }

    public string TextureHint => _mob.Model switch
    {
        MobModel.Zombie or MobModel.Skeleton or MobModel.Villager or MobModel.Witch or MobModel.Blaze =>
            "Image au format de la créature d'origine (64 x 64 pour un zombie), sinon elle s'affichera mal.",
        _ => "Image au format de la texture de la créature d'origine (souvent 64 x 32), sinon elle s'affichera mal.",
    };

    public bool UseCustomHitbox
    {
        get => _mob.UseCustomHitbox;
        set => ChangeIfDifferent("Modifier la taille", nameof(MobAsset.UseCustomHitbox), () => _mob.UseCustomHitbox, v => _mob.UseCustomHitbox = v, value);
    }

    public double Width
    {
        get => _mob.Width;
        set => ChangeNumber("Modifier la largeur", nameof(MobAsset.Width), value, () => _mob.Width, v => _mob.Width = v,
            v => (float)Math.Clamp(v, MobAsset.MinHitbox, MobAsset.MaxHitbox));
    }

    public double Height
    {
        get => _mob.Height;
        set => ChangeNumber("Modifier la hauteur", nameof(MobAsset.Height), value, () => _mob.Height, v => _mob.Height = v,
            v => (float)Math.Clamp(v, MobAsset.MinHitbox, MobAsset.MaxHitbox));
    }

    public string HitboxHint
    {
        get
        {
            (float width, float height) = MobModelInfo.DefaultHitbox(_mob.Model);
            return string.Create(CultureInfo.CurrentCulture, $"Taille d'origine du modèle : {width:0.##} x {height:0.##} blocs (largeur x hauteur).");
        }
    }

    // ----- Attributs -----

    public MobKind Kind
    {
        get => _mob.Kind;
        set => ChangeIfDifferent("Modifier la famille", nameof(MobAsset.Kind), () => _mob.Kind, v => _mob.Kind = v, value);
    }

    public bool IsAnimal => _mob.Kind == MobKind.Animal;

    public double Health
    {
        get => _mob.Health;
        set => ChangeNumber("Modifier la vie", nameof(MobAsset.Health), value, () => _mob.Health, v => _mob.Health = v,
            v => Math.Clamp(v, 1, MobAsset.MaxHealth));
    }

    public double Armor
    {
        get => _mob.Armor;
        set => ChangeNumber("Modifier l'armure", nameof(MobAsset.Armor), value, () => _mob.Armor, v => _mob.Armor = v,
            v => Math.Clamp(v, 0, MobAsset.MaxArmor));
    }

    public double MovementSpeed
    {
        get => _mob.MovementSpeed;
        set => ChangeNumber("Modifier la vitesse", nameof(MobAsset.MovementSpeed), value, () => _mob.MovementSpeed, v => _mob.MovementSpeed = v,
            v => Math.Clamp(v, 0, MobAsset.MaxMovementSpeed));
    }

    public double AttackDamage
    {
        get => _mob.AttackDamage;
        set => ChangeNumber("Modifier les dégâts", nameof(MobAsset.AttackDamage), value, () => _mob.AttackDamage, v => _mob.AttackDamage = v,
            v => Math.Clamp(v, 0, MobAsset.MaxAttackDamage));
    }

    public double FollowRange
    {
        get => _mob.FollowRange;
        set => ChangeNumber("Modifier la portée de suivi", nameof(MobAsset.FollowRange), value, () => _mob.FollowRange, v => _mob.FollowRange = v,
            v => Math.Clamp(v, 1, MobAsset.MaxFollowRange));
    }

    public double KnockbackResistance
    {
        get => _mob.KnockbackResistance;
        set => ChangeNumber("Modifier la résistance au recul", nameof(MobAsset.KnockbackResistance), value, () => _mob.KnockbackResistance,
            v => _mob.KnockbackResistance = v, v => Math.Clamp(v, 0, 1));
    }

    public double Experience
    {
        get => _mob.Experience;
        set => ChangeNumber("Modifier l'expérience", nameof(MobAsset.Experience), value, () => _mob.Experience, v => _mob.Experience = v,
            v => Math.Clamp((int)Math.Round(v), 0, MobAsset.MaxExperience));
    }

    public bool FireImmune
    {
        get => _mob.FireImmune;
        set => ChangeIfDifferent("Modifier la résistance au feu", nameof(MobAsset.FireImmune), () => _mob.FireImmune, v => _mob.FireImmune = v, value);
    }

    public bool BurnsInDaylight
    {
        get => _mob.BurnsInDaylight;
        set => ChangeIfDifferent("Modifier « brûle au soleil »", nameof(MobAsset.BurnsInDaylight), () => _mob.BurnsInDaylight, v => _mob.BurnsInDaylight = v, value);
    }

    public bool Persistent
    {
        get => _mob.Persistent;
        set => ChangeIfDifferent("Modifier « ne disparaît jamais »", nameof(MobAsset.Persistent), () => _mob.Persistent, v => _mob.Persistent = v, value);
    }

    // ----- IA -----

    public ObservableCollection<MobGoalViewModel> Goals { get; } = [];

    public int GoalCount => _mob.Goals.Count;

    public MobGoalKind NewGoalKind
    {
        get => _newGoalKind;
        set => SetProperty(ref _newGoalKind, value);
    }

    public ICommand AddGoalCommand { get; }

    public ReferencePickerViewModel BreedingItem { get; }

    public MobGoal GetGoal(int index) => _mob.Goals[index];

    /// <summary>Remplace un comportement ; <paramref name="seal"/> termine l'étape d'annulation (choix discret).</summary>
    public void UpdateGoal(int index, Func<MobGoal, MobGoal> update, bool seal)
    {
        var goals = _mob.Goals.ToList();
        goals[index] = update(goals[index]);
        SetGoals("Modifier l'IA", goals, seal);
    }

    public void MoveGoal(int index, int offset)
    {
        int target = index + offset;
        if (target < 0 || target >= _mob.Goals.Count)
        {
            return;
        }

        var goals = _mob.Goals.ToList();
        (goals[index], goals[target]) = (goals[target], goals[index]);
        SetGoals("Changer la priorité d'un comportement", goals, seal: true);
    }

    public void RemoveGoal(int index)
    {
        var goals = _mob.Goals.ToList();
        goals.RemoveAt(index);
        SetGoals("Retirer un comportement", goals, seal: true);
    }

    // ----- Sons et butin -----

    public ReferencePickerViewModel AmbientSound { get; }

    public ReferencePickerViewModel HurtSound { get; }

    public ReferencePickerViewModel DeathSound { get; }

    public ObservableCollection<MobDropViewModel> Drops { get; } = [];

    public ICommand AddDropCommand { get; }

    public MobDrop GetDrop(int index) => _mob.Drops[index];

    public void UpdateDrop(int index, Func<MobDrop, MobDrop> update)
    {
        var drops = _mob.Drops.ToList();
        drops[index] = update(drops[index]);
        Change("Modifier le butin", nameof(MobAsset.Drops), () => _mob.Drops, v => _mob.Drops = v, drops);
    }

    public void RemoveDrop(int index)
    {
        var drops = _mob.Drops.ToList();
        drops.RemoveAt(index);
        Change("Retirer un butin", nameof(MobAsset.Drops), () => _mob.Drops, v => _mob.Drops = v, drops);
        Context.History.Seal();
    }

    // ----- Apparition -----

    public bool HasSpawnEgg
    {
        get => _mob.HasSpawnEgg;
        set => ChangeIfDifferent("Modifier l'œuf d'apparition", nameof(MobAsset.HasSpawnEgg), () => _mob.HasSpawnEgg, v => _mob.HasSpawnEgg = v, value);
    }

    /// <summary>Couleur de fond de l'œuf, saisie en hexadécimal ("#3A6B1F").</summary>
    public string EggPrimaryText
    {
        get => _eggPrimaryText;
        set => SetColorText(ref _eggPrimaryText, value, nameof(EggPrimaryText), nameof(MobAsset.EggPrimaryColor),
            () => _mob.EggPrimaryColor, v => _mob.EggPrimaryColor = v);
    }

    public string EggSecondaryText
    {
        get => _eggSecondaryText;
        set => SetColorText(ref _eggSecondaryText, value, nameof(EggSecondaryText), nameof(MobAsset.EggSecondaryColor),
            () => _mob.EggSecondaryColor, v => _mob.EggSecondaryColor = v);
    }

    /// <summary>Couleurs au format "#RRGGBB" pour l'aperçu (toujours valides).</summary>
    public string EggPrimaryPreview => ColorText(_mob.EggPrimaryColor);

    public string EggSecondaryPreview => ColorText(_mob.EggSecondaryColor);

    public bool SpawnsNaturally
    {
        get => _mob.SpawnsNaturally;
        set => ChangeIfDifferent("Modifier l'apparition naturelle", nameof(MobAsset.SpawnsNaturally), () => _mob.SpawnsNaturally, v => _mob.SpawnsNaturally = v, value);
    }

    public IReadOnlyList<ToggleOptionViewModel> BiomeOptions { get; }

    public double SpawnWeight
    {
        get => _mob.SpawnWeight;
        set => ChangeNumber("Modifier la fréquence d'apparition", nameof(MobAsset.SpawnWeight), value, () => _mob.SpawnWeight, v => _mob.SpawnWeight = v,
            v => Math.Clamp((int)Math.Round(v), 1, MobAsset.MaxSpawnWeight));
    }

    public double SpawnMinGroup
    {
        get => _mob.SpawnMinGroup;
        set => ChangeNumber("Modifier la taille des groupes", nameof(MobAsset.SpawnMinGroup), value, () => _mob.SpawnMinGroup, v => _mob.SpawnMinGroup = v,
            v => Math.Clamp((int)Math.Round(v), 1, MobAsset.MaxGroupSize));
    }

    public double SpawnMaxGroup
    {
        get => _mob.SpawnMaxGroup;
        set => ChangeNumber("Modifier la taille des groupes", nameof(MobAsset.SpawnMaxGroup), value, () => _mob.SpawnMaxGroup, v => _mob.SpawnMaxGroup = v,
            v => Math.Clamp((int)Math.Round(v), 1, MobAsset.MaxGroupSize));
    }

    public string SummonCommand => $"/summon {FullResourceId}";

    public override void OnProjectContentChanged()
    {
        base.OnProjectContentChanged();
        BreedingItem.Refresh();
        AmbientSound.Refresh();
        HurtSound.Refresh();
        DeathSound.Refresh();

        // Le registre prévient avant ce ViewModel : si l'IA ou le butin de ce mob vient de
        // changer de taille (annulation), les lignes sont d'abord remises en accord.
        SyncGoals();
        SyncDrops();
    }

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);

        // La plupart des propriétés portent le même nom ici et dans l'asset.
        OnPropertyChanged(propertyName == nameof(MobAsset.Model) ? nameof(MobModel) : propertyName);
        switch (propertyName)
        {
            case nameof(MobAsset.Model):
                OnPropertyChanged(nameof(TextureHint));
                OnPropertyChanged(nameof(HitboxHint));
                break;

            case nameof(MobAsset.Kind):
                OnPropertyChanged(nameof(IsAnimal));
                foreach (MobGoalViewModel goal in Goals)
                {
                    goal.Refresh();
                }

                break;

            case nameof(MobAsset.Goals):
                SyncGoals();
                break;

            case nameof(MobAsset.Drops):
                SyncDrops();
                break;

            case nameof(MobAsset.BreedingItem):
                BreedingItem.Refresh();
                break;

            case nameof(MobAsset.AmbientSound):
                AmbientSound.Refresh();
                break;

            case nameof(MobAsset.HurtSound):
                HurtSound.Refresh();
                break;

            case nameof(MobAsset.DeathSound):
                DeathSound.Refresh();
                break;

            case nameof(MobAsset.SpawnBiomes):
                foreach (ToggleOptionViewModel option in BiomeOptions)
                {
                    option.Refresh();
                }

                break;

            case nameof(MobAsset.EggPrimaryColor):
                RefreshColorText(ref _eggPrimaryText, _mob.EggPrimaryColor, nameof(EggPrimaryText));
                OnPropertyChanged(nameof(EggPrimaryPreview));
                break;

            case nameof(MobAsset.EggSecondaryColor):
                RefreshColorText(ref _eggSecondaryText, _mob.EggSecondaryColor, nameof(EggSecondaryText));
                OnPropertyChanged(nameof(EggSecondaryPreview));
                break;

            case nameof(Asset.ResourceId):
                OnPropertyChanged(nameof(SummonCommand));
                break;
        }
    }

    private static string ColorText(int rgb) => $"#{rgb & 0xFFFFFF:X6}";

    private static bool TryParseColor(string text, out int rgb)
    {
        string hex = text.Trim().TrimStart('#');
        return int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb) && hex.Length == 6;
    }

    private ReferencePickerViewModel SoundPicker(string propertyName, Func<ContentReference?> getter, Action<ContentReference?> setter) =>
        new(Context, ReferenceDomain.Sound, getter, v => Change("Modifier un son", propertyName, getter, setter, v));

    private void SetBiome(SpawnBiome biome, bool included)
    {
        if (_mob.SpawnBiomes.Contains(biome) == included)
        {
            return;
        }

        var biomes = new HashSet<SpawnBiome>(_mob.SpawnBiomes);
        if (included)
        {
            biomes.Add(biome);
        }
        else
        {
            biomes.Remove(biome);
        }

        Change("Modifier les biomes", nameof(MobAsset.SpawnBiomes), () => _mob.SpawnBiomes, v => _mob.SpawnBiomes = v, biomes);
        Context.History.Seal();
    }

    private void SetColorText(ref string field, string? value, string viewProperty, string modelProperty, Func<int> getter, Action<int> setter)
    {
        if (!SetProperty(ref field, value ?? string.Empty, viewProperty))
        {
            return;
        }

        if (!TryParseColor(field, out int rgb))
        {
            SetError(viewProperty, "Couleur hexadécimale attendue, par exemple #3A6B1F.");
            return;
        }

        ClearErrors(viewProperty);
        if (rgb != getter())
        {
            Change("Modifier une couleur de l'œuf", modelProperty, getter, setter, rgb);
        }
    }

    private void RefreshColorText(ref string field, int rgb, string viewProperty)
    {
        if (!TryParseColor(field, out int typed) || typed != rgb)
        {
            field = ColorText(rgb);
            ClearErrors(viewProperty);
            OnPropertyChanged(viewProperty);
        }
    }

    private void AddGoal()
    {
        SetGoals("Ajouter un comportement", [.. _mob.Goals, MobGoal.Create(NewGoalKind)], seal: true);
    }

    private void AddDrop()
    {
        Change("Ajouter un butin", nameof(MobAsset.Drops), () => _mob.Drops, v => _mob.Drops = v,
            [.. _mob.Drops, new MobDrop(DefaultReferences.Bone, 1, 1)]);
        Context.History.Seal();
    }

    private void SetGoals(string description, IReadOnlyList<MobGoal> goals, bool seal)
    {
        Change(description, nameof(MobAsset.Goals), () => _mob.Goals, v => _mob.Goals = v, goals);
        if (seal)
        {
            Context.History.Seal();
        }
    }

    /// <summary>
    /// Les lignes lisent le comportement de leur position : tant que le nombre ne change
    /// pas, on se contente de les réafficher (le champ en cours de saisie garde le focus) ;
    /// sinon la liste est reconstruite.
    /// </summary>
    private void SyncGoals()
    {
        if (Goals.Count == _mob.Goals.Count)
        {
            foreach (MobGoalViewModel goal in Goals)
            {
                goal.Refresh();
            }
        }
        else
        {
            RebuildGoals();
        }

        OnPropertyChanged(nameof(GoalCount));
    }

    private void RebuildGoals()
    {
        Goals.Clear();
        for (int i = 0; i < _mob.Goals.Count; i++)
        {
            Goals.Add(new MobGoalViewModel(this, Context, i));
        }
    }

    private void SyncDrops()
    {
        if (Drops.Count == _mob.Drops.Count)
        {
            foreach (MobDropViewModel drop in Drops)
            {
                drop.Refresh();
            }
        }
        else
        {
            RebuildDrops();
        }
    }

    private void RebuildDrops()
    {
        Drops.Clear();
        for (int i = 0; i < _mob.Drops.Count; i++)
        {
            Drops.Add(new MobDropViewModel(this, Context, i));
        }
    }
}
