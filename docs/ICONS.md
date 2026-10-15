# Style des icônes

Les icônes de Mine Engine suivent le style de VS Code et de Windows 11 : simples, au trait, monochromes et discrètes. Elles servent à repérer une action ou un type d'élément, pas à décorer.

## Où sont les icônes

- **`src/MineEngine.Editor/Themes/Icons.xaml`** : la police d'icônes (`IconFont`), les glyphes (`Glyph.Save`, `Glyph.Run`...), les géométries vectorielles (`BlockGeometry`, `ItemGeometry`, `MobGeometry`, `RecipeGeometry`, `FunctionGeometry`, `ClassGeometry`, `ForgeGeometry`...) et les styles (`IconGlyph`, `IconPath`, `IconButton`, `LogoPath`...). Une clé a un seul sens dans toute l'application.
- **Dans le XAML** : `Text="{StaticResource Glyph.Save}"` pour un glyphe, `<Path Style="{StaticResource IconPath}" Data="{StaticResource BlockGeometry}" />` pour une icône vectorielle.
- **Depuis un view model** : il donne le nom de l'icône (`TypeIcon => "BlockGeometry"`), jamais un code de caractère ; la vue l'affiche avec `<views:IconView Icon="{Binding TypeIcon}" IconSize="16" />`, qui accepte un glyphe comme une géométrie.
- **Nouvelle icône** : l'ajouter dans `Icons.xaml` (repère 16 × 16, contour seul), puis l'utiliser par sa clé.

## Règles

- **Source** : la police système `Segoe Fluent Icons, Segoe MDL2 Assets` (glyphes au code Unicode, par exemple `E8A9`). Une icône absente de la police est dessinée en `Path` vectoriel, comme le cube du logo (`CubeGeometry`).
- **Trait** : des contours fins, sans remplissage plein ni dégradé, ombre, relief ou effet 3D. Pour un `Path`, l'épaisseur du trait est de 1,4 à 1,6 px à 16 px de côté.
- **Taille** : 12 px dans les boutons d'outils (`IconButton`), 13 px à côté d'un texte (`IconGlyph`), 15 à 16 px pour un type d'élément ou le logo. La zone cliquable autour d'un glyphe fait au moins 24 px.
- **Couleur** : une seule couleur par icône, toujours tirée d'un pinceau du thème (`DynamicResource`), jamais d'une couleur écrite en dur. Ainsi, l'icône suit les thèmes sombre et clair.
  - repos : `HintBrush` (gris) ;
  - survol, ou état actif (panneau affiché, option cochée) : `StrongTextBrush` ;
  - mise en avant (logo, élément sélectionné) : `AccentLineBrush` (bleu) ;
  - sur un badge `BadgeBrush` : `BadgeTextBrush`.
- **État** : un changement d'état se montre par un changement de couleur, sans changer l'icône ni sa taille. Une action désactivée est atténuée par son opacité.
- **Sens** : une icône désigne toujours la même chose dans toute l'application. Un bouton qui n'a qu'une icône porte une infobulle avec son nom et son raccourci, par exemple « Explorateur (Ctrl+B) ».
- **Exception** : les textures et icônes des éléments du mod (items, blocs) sont du pixel art Minecraft. Elles sont affichées sans lissage (`NearestNeighbor`) et ne sont jamais recolorées.
- **Interdits** : les emojis, les images bitmap pour l'interface, et le mélange de plusieurs familles d'icônes.
