using System.Collections.ObjectModel;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Commands;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.Commands;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// Éditeur d'un item : visuel, type (outil, arme, armure), propriétés, nourriture, données
/// (combustible, tags, item rendu) et événements reliés à des graphes nodaux.
/// </summary>
public sealed partial class ItemAssetViewModel : InventoryAssetViewModel
{
    private const int TicksPerSecond = 20;

    private readonly ItemAsset _item;
    private string _tagsText;

    public ItemAssetViewModel(ItemAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _item = model;
        _tagsText = TagsToText(model.ItemTags);
        CraftRemainder = new ReferencePickerViewModel(
            context, ReferenceDomain.Item, () => _item.CraftRemainder,
            v => Change(Tr.T("Change returned item"), nameof(ItemAsset.CraftRemainder), () => _item.CraftRemainder, x => _item.CraftRemainder = x, v));
        RepairItemPicker = new ReferencePickerViewModel(
            context, ReferenceDomain.Item, () => _item.RepairItem,
            v => Change(Tr.T("Change the repair item"), nameof(ItemAsset.RepairItem), () => _item.RepairItem, x => _item.RepairItem = x, v));
        EatResultPicker = new ReferencePickerViewModel(
            context, ReferenceDomain.Item, () => _item.EatResult,
            v => Change(Tr.T("Change the item left once eaten"), nameof(ItemAsset.EatResult), () => _item.EatResult, x => _item.EatResult = x, v));
        AmmoPicker = new ReferencePickerViewModel(
            context, ReferenceDomain.Item, () => _item.Ranged.Ammo,
            v => Change(Tr.T("Change the ammunition"), nameof(ItemAsset.Ranged), () => _item.Ranged, x => _item.Ranged = x, _item.Ranged with { Ammo = v }));
        GuiTextureSlot = new TextureSlotViewModel(model, context, nameof(ItemAsset.GuiTextureId), Tr.T("Texture in the inventories"),
            Tr.T("The 3D model"), () => model.GuiTextureId, v => model.GuiTextureId = v);
        MethodSlots = ItemMethodSlots.All.ToDictionary(s => s.Key, s => new ElementMethodViewModel(
            model, context, s, () => _item.Methods, v => _item.Methods = v));
        AddFoodEffectCommand = new RelayCommand(AddFoodEffect);
        Events = [.. Enum.GetValues<ItemEventKind>().Select(k => new ItemEventViewModel(this, k))];
        ItemEventViewModel[] Group(params ItemEventKind[] kinds) => [.. kinds.Select(k => Events.Single(e => e.Kind == k))];
        BasicEvents = Group(
            ItemEventKind.RightClick, ItemEventKind.UseOnBlock, ItemEventKind.UseOnEntity, ItemEventKind.HitEntity, ItemEventKind.BlockMined,
            ItemEventKind.HeldTick, ItemEventKind.Broken, ItemEventKind.Crafted, ItemEventKind.Dropped, ItemEventKind.PickedUp);
        FoodEvents = Group(ItemEventKind.EatingStarted, ItemEventKind.EatingTick, ItemEventKind.EatingStopped, ItemEventKind.Consumed);
        ToolEvents = Group(ItemEventKind.ToolSwingTick, ItemEventKind.ToolSwung);
        RangedEvents = Group(ItemEventKind.ChargeStarted, ItemEventKind.ChargingTick, ItemEventKind.Charged, ItemEventKind.Shot);
        RebuildFoodEffects();
        RefreshEvents();
    }

    public int MaxDurability => ItemAsset.MaxDurability;

    public int MaxNutrition => ItemAsset.MaxNutrition;

    public double MaxSaturation => ItemAsset.MaxSaturation;

    public double Durability
    {
        get => _item.Durability;
        set => ChangeNumber(Tr.T("Change durability"), nameof(ItemAsset.Durability), value, () => _item.Durability, v => _item.Durability = v,
            v => Math.Clamp((int)Math.Round(v), 0, ItemAsset.MaxDurability));
    }

    /// <summary>Un outil ou une armure tire sa durabilité de son matériau.</summary>
    public bool CanEditDurability => !_item.Kind.IsEquipment();

    /// <summary>La taille de pile n'a pas de sens pour un item à durabilité, un outil ou une armure (toujours 1).</summary>
    public override bool CanEditStackSize => !_item.HasDurability && !_item.Kind.IsEquipment();

    public override string StackSizeHint => !CanEditStackSize
        ? Tr.T("Always 1: an item with durability, a tool or an armor does not stack.")
        : base.StackSizeHint;

    public bool HasGlint
    {
        get => _item.HasGlint;
        set => ChangeIfDifferent(Tr.T("Change glint effect"), nameof(ItemAsset.HasGlint), () => _item.HasGlint, v => _item.HasGlint = v, value);
    }

    public bool Handheld
    {
        get => _item.Handheld;
        set => ChangeIfDifferent(Tr.T("Change handheld rendering"), nameof(ItemAsset.Handheld), () => _item.Handheld, v => _item.Handheld = v, value);
    }

    // ----- Type d'item -----

    public IReadOnlyList<Choice<ItemKind>> KindChoices => GameDataChoices.ItemKinds;

    public IReadOnlyList<Choice<ItemTier>> TierChoices => GameDataChoices.ItemTiers;

    public IReadOnlyList<Choice<ArmorMaterialKind>> ArmorMaterialChoices => GameDataChoices.ArmorMaterials;

    /// <summary>
    /// Changer de type règle aussi, en une seule étape, les dégâts et la vitesse d'attaque
    /// de Minecraft pour ce type, et la prise en main (un outil se tient en diagonale).
    /// </summary>
    public ItemKind Kind
    {
        get => _item.Kind;
        set
        {
            if (value == _item.Kind)
            {
                return;
            }

            (float damage, float speed) = value.DefaultCombat();
            var commands = new List<IUndoableCommand>
            {
                Property(nameof(ItemAsset.Kind), () => _item.Kind, v => _item.Kind = v, value),
                Property(nameof(ItemAsset.Handheld), () => _item.Handheld, v => _item.Handheld = v, value.IsTool()),
            };
            if (value.IsTool())
            {
                commands.Add(Property(nameof(ItemAsset.AttackDamage), () => _item.AttackDamage, v => _item.AttackDamage = v, damage));
                commands.Add(Property(nameof(ItemAsset.AttackSpeed), () => _item.AttackSpeed, v => _item.AttackSpeed = v, speed));
            }

            Context.History.Execute(new CompositeCommand(Tr.T("Change item type"), commands));
            Context.History.Seal();
        }
    }

    public bool IsTool => _item.Kind.IsTool();

    public bool IsArmor => _item.Kind.IsArmor();

    public ItemTier Tier
    {
        get => _item.Tier;
        set => ChangeIfDifferent(Tr.T("Change material"), nameof(ItemAsset.Tier), () => _item.Tier, v => _item.Tier = v, value);
    }

    public ArmorMaterialKind ArmorMaterial
    {
        get => _item.ArmorMaterial;
        set => ChangeIfDifferent(Tr.T("Change material"), nameof(ItemAsset.ArmorMaterial), () => _item.ArmorMaterial, v => _item.ArmorMaterial = v, value);
    }

    public double AttackDamage
    {
        get => Math.Round(_item.AttackDamage, 2);
        set => ChangeNumber(Tr.T("Change damage"), nameof(ItemAsset.AttackDamage), value, () => _item.AttackDamage, v => _item.AttackDamage = v, v => (float)v);
    }

    public double AttackSpeed
    {
        get => Math.Round(_item.AttackSpeed, 2);
        set => ChangeNumber(Tr.T("Change attack speed"), nameof(ItemAsset.AttackSpeed), value, () => _item.AttackSpeed, v => _item.AttackSpeed = v, v => (float)v);
    }

    public double MaxAttack => ItemAsset.MaxAttack;

    // ----- Données : item rendu, réparation, combustible, tags -----

    public ReferencePickerViewModel CraftRemainder { get; }

    public bool NoRepair
    {
        get => _item.NoRepair;
        set => ChangeIfDifferent(Tr.T("Change repair"), nameof(ItemAsset.NoRepair), () => _item.NoRepair, v => _item.NoRepair = v, value);
    }

    /// <summary>Durée de combustion en secondes (charbon : 80) ; 0 = pas un combustible.</summary>
    public double BurnSeconds
    {
        get => _item.BurnTime / (double)TicksPerSecond;
        set => ChangeNumber(Tr.T("Change burn time"), nameof(ItemAsset.BurnTime), value, () => _item.BurnTime, v => _item.BurnTime = v,
            v => Math.Clamp((int)Math.Round(v * TicksPerSecond), 0, ItemAsset.MaxBurnTime));
    }

    public double MaxBurnSeconds => ItemAsset.MaxBurnTime / (double)TicksPerSecond;

    /// <summary>Tags, un par ligne ("c:gems/ruby"). Appliqués dès que toutes les lignes sont valides.</summary>
    public string TagsText
    {
        get => _tagsText;
        set
        {
            if (!SetProperty(ref _tagsText, value ?? string.Empty))
            {
                return;
            }

            var tags = new List<NamespacedId>();
            foreach (string line in _tagsText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!NamespacedId.TryParse(line.TrimStart('#'), out NamespacedId? tag))
                {
                    SetError(nameof(TagsText), Tr.F("Invalid tag: \"{0}\" (example: c:gems/ruby).", line));
                    return;
                }

                tags.Add(tag!);
            }

            ClearErrors(nameof(TagsText));
            if (!tags.SequenceEqual(_item.ItemTags))
            {
                Change(Tr.T("Change tags"), nameof(ItemAsset.ItemTags), () => _item.ItemTags, v => _item.ItemTags = v, tags);
            }
        }
    }

    // ----- Nourriture -----

    public bool IsFood
    {
        get => _item.IsFood;
        set => ChangeIfDifferent(Tr.T("Make edible"), nameof(ItemAsset.IsFood), () => _item.IsFood, v => _item.IsFood = v, value);
    }

    public double Nutrition
    {
        get => _item.Nutrition;
        set => ChangeNumber(Tr.T("Change nutrition"), nameof(ItemAsset.Nutrition), value, () => _item.Nutrition, v => _item.Nutrition = v,
            v => Math.Clamp((int)Math.Round(v), 0, ItemAsset.MaxNutrition));
    }

    public double Saturation
    {
        get => _item.Saturation;
        set => ChangeNumber(Tr.T("Change saturation"), nameof(ItemAsset.Saturation), value, () => _item.Saturation, v => _item.Saturation = v,
            v => (float)Math.Clamp(v, 0d, ItemAsset.MaxSaturation));
    }

    public bool AlwaysEdible
    {
        get => _item.AlwaysEdible;
        set => ChangeIfDifferent(Tr.T("Change \"always edible\""), nameof(ItemAsset.AlwaysEdible), () => _item.AlwaysEdible, v => _item.AlwaysEdible = v, value);
    }

    public bool IsMeat
    {
        get => _item.IsMeat;
        set => ChangeIfDifferent(Tr.T("Change \"meat\""), nameof(ItemAsset.IsMeat), () => _item.IsMeat, v => _item.IsMeat = v, value);
    }

    public ObservableCollection<FoodEffectViewModel> FoodEffects { get; } = [];

    public ICommand AddFoodEffectCommand { get; }

    public FoodEffect GetFoodEffect(int index) => _item.FoodEffects[index];

    public void UpdateFoodEffect(int index, Func<FoodEffect, FoodEffect> update)
    {
        List<FoodEffect> effects = [.. _item.FoodEffects];
        effects[index] = update(effects[index]);
        Change(Tr.T("Change an effect"), nameof(ItemAsset.FoodEffects), () => _item.FoodEffects, v => _item.FoodEffects = v, effects);
    }

    public void RemoveFoodEffect(int index)
    {
        List<FoodEffect> effects = [.. _item.FoodEffects];
        effects.RemoveAt(index);
        Change(Tr.T("Remove an effect"), nameof(ItemAsset.FoodEffects), () => _item.FoodEffects, v => _item.FoodEffects = v, effects);
        Context.History.Seal();
    }

    // ----- Événements -----

    /// <summary>Événements de l'item (clic droit, frappe, fabrication...), chacun relié ou non à un graphe.</summary>
    public IReadOnlyList<ItemEventViewModel> Events { get; }

    /// <summary>The events in the groups of item.md (V0.7.12): basic item, food, tool, ranged item.</summary>
    public IReadOnlyList<ItemEventViewModel> BasicEvents { get; }

    public IReadOnlyList<ItemEventViewModel> FoodEvents { get; }

    public IReadOnlyList<ItemEventViewModel> ToolEvents { get; }

    public IReadOnlyList<ItemEventViewModel> RangedEvents { get; }

    public string FoodEventsHint => _item.IsFood
        ? Tr.T("They trigger while the item is eaten or drunk.")
        : Tr.T("They only trigger for a food item (Food properties tab).");

    public string RangedEventsHint => _item.Ranged.Kind is RangedItemKind.Bow or RangedItemKind.Crossbow or RangedItemKind.Throw
        ? Tr.T("\"Charged\" only triggers for an item that shoots on release.")
        : Tr.T("They only trigger for an item that shoots (Ranged item tab, not a fishing rod).");

    /// <summary>
    /// Relie un événement : un nœud événement est ajouté au graphe d'événements de l'item
    /// (créé au besoin), en une seule étape annulable, puis le graphe s'ouvre sur ce nœud.
    /// </summary>
    public void AddItemEvent(ItemEventKind kind)
    {
        string itemRef = ContentReference.ToAsset(_item.Id).ToStorageText();
        GraphAsset? graph = Context.Project.Assets.OfType<GraphAsset>()
            .FirstOrDefault(g => g.Kind == GraphKind.Events && g.Graph.Nodes.Any(n => IsOwnEvent(n, itemRef)));
        int existing = graph?.Graph.Nodes.Count(n => NodeCatalog.Find(n.Kind)?.Category == NodeCategory.Event) ?? 0;
        GraphNode node = GraphNode.Create(NodeCatalog.ForItemEvent(kind).Kind, 0, existing * 220).WithValue("item", itemRef);
        string description = Tr.F("Connect \"{0}\"", NodeCatalog.ForItemEvent(kind).Title);

        if (graph is null)
        {
            ResourceId id = Context.Project.Assets.CreateUniqueResourceId(_item.ResourceId.Value + "_events", AssetType.Graph);
            graph = new GraphAsset(Guid.NewGuid(), id, Tr.F("{0} events", Header)) { Folder = _item.Folder };
            graph.Graph = graph.Graph.AddNode(node);
            Context.History.Execute(new AddAssetCommand(Context.Project.Assets, graph, Tr.T("Graph")));
        }
        else
        {
            GraphAsset target = graph;
            Context.History.Execute(new PropertyChangeCommand<NodeGraph>(
                description, target, nameof(GraphAsset.Graph), () => target.Graph, v => target.Graph = v, target.Graph.AddNode(node)));
        }

        Context.History.Seal();
        Context.Navigation.Open(graph, node.Id);
    }

    public void OpenItemEvent(GraphAsset graph, GraphNode node) => Context.Navigation.Open(graph, node.Id);

    public override void OnProjectContentChanged()
    {
        base.OnProjectContentChanged();
        CraftRemainder.Refresh();
        RepairItemPicker.Refresh();
        EatResultPicker.Refresh();
        GuiTextureSlot.Refresh();
        AmmoPicker.Refresh();
        RefreshMethods();
        OnPropertyChanged(nameof(FunctionChoices));
        OnPropertyChanged(nameof(ValueFunctionChoices));
        RefreshEvents();
    }

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        OnSectionPropertyChanged(propertyName);
        OnAdvancedPropertyChanged(propertyName);
        switch (propertyName)
        {
            case nameof(ItemAsset.Durability):
                OnPropertyChanged(nameof(CanEditStackSize));
                OnPropertyChanged(nameof(StackSizeHint));
                break;
            case nameof(ItemAsset.Kind):
                OnPropertyChanged(nameof(IsTool));
                OnPropertyChanged(nameof(IsArmor));
                OnPropertyChanged(nameof(CanEditDurability));
                OnPropertyChanged(nameof(CanEditStackSize));
                OnPropertyChanged(nameof(StackSizeHint));
                break;
            case nameof(ItemAsset.BurnTime):
                OnPropertyChanged(nameof(BurnSeconds));
                break;
            case nameof(ItemAsset.CraftRemainder):
                CraftRemainder.Refresh();
                break;
            case nameof(ItemAsset.ItemTags):
                // Le texte saisi n'est remplacé que s'il ne correspond plus aux tags (annulation), pour ne pas gêner la frappe.
                if (TagsToText(_item.ItemTags) != NormalizeTags(_tagsText))
                {
                    _tagsText = TagsToText(_item.ItemTags);
                    ClearErrors(nameof(TagsText));
                    OnPropertyChanged(nameof(TagsText));
                }

                break;
            case nameof(ItemAsset.FoodEffects):
                SyncFoodEffects();
                break;
            case nameof(ItemAsset.IsFood):
                OnPropertyChanged(nameof(FoodEventsHint));
                break;
        }
    }

    private static bool IsOwnEvent(GraphNode node, string itemRef) => NodeCatalog.Find(node.Kind)?.ItemEvent is not null && node.GetValue("item") == itemRef;

    private void RefreshEvents()
    {
        string itemRef = ContentReference.ToAsset(_item.Id).ToStorageText();
        List<(GraphAsset Graph, GraphNode Node)> links = [.. Context.Project.Assets.OfType<GraphAsset>()
            .SelectMany(g => g.Graph.Nodes.Where(n => IsOwnEvent(n, itemRef)).Select(n => (g, n)))];
        foreach (ItemEventViewModel row in Events)
        {
            row.SetLinks([.. links.Where(l => NodeCatalog.Find(l.Node.Kind)?.ItemEvent == row.Kind)]);
        }
    }

    private void AddFoodEffect()
    {
        Change(Tr.T("Add an effect"), nameof(ItemAsset.FoodEffects), () => _item.FoodEffects, v => _item.FoodEffects = v,
            [.. _item.FoodEffects, new FoodEffect(FoodEffectKind.Regeneration, 5, 0, 1f)]);
        Context.History.Seal();
    }

    private void SyncFoodEffects()
    {
        if (FoodEffects.Count == _item.FoodEffects.Count)
        {
            foreach (FoodEffectViewModel effect in FoodEffects)
            {
                effect.Refresh();
            }
        }
        else
        {
            RebuildFoodEffects();
        }
    }

    private void RebuildFoodEffects()
    {
        FoodEffects.Clear();
        for (int i = 0; i < _item.FoodEffects.Count; i++)
        {
            FoodEffects.Add(new FoodEffectViewModel(this, i));
        }
    }

    private PropertyChangeCommand<T> Property<T>(string name, Func<T> getter, Action<T> setter, T value) =>
        new(Tr.T("Change item type"), Model, name, getter, setter, value);

    private static string TagsToText(IEnumerable<NamespacedId> tags) => string.Join(Environment.NewLine, tags.Select(t => t.ToString()));

    private static string NormalizeTags(string text) =>
        string.Join(Environment.NewLine, text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(l => l.TrimStart('#')));
}
