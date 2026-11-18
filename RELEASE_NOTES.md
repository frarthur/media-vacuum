# MediaVacuum — Notes de version

## v0.2.0

### Nouveautés

- **File de téléchargement** : chaque URL est analysée avant le téléchargement. Les vidéos apparaissent dans une liste avec miniature, titre, durée, taille, statut et progression.
- **Playlists et chaînes** : une ligne par vidéo, avec sélection individuelle, progression par élément et taille totale de la sélection.
- **Miniatures** : affichage dans la file, avec cache local. La miniature peut être incrustée dans le média et/ou conservée à côté du fichier.
- **Tailles** : estimation avant téléchargement, affinée pendant le téléchargement et complétée à la fin. L'espace libre du disque de destination est vérifié avant de commencer, avec une marge de sécurité.
- **Formats** : presets de qualité corrigés (`bv*[height<=…]+ba/b[… ]`), conteneurs MP4/MKV/WebM, réencodage MP4 optionnel, extraction audio (MP3, M4A, AAC, FLAC, Opus, Vorbis, WAV, ALAC) et sélecteur de format avancé.
- **Noms de fichiers propres** : suppression des caractères Windows invalides, des espaces multiples et des points finaux. Les playlists utilisent un dossier dédié et une numérotation `01 - Titre`.
- **Thèmes** : Système, Clair et Sombre, appliqués en direct.
- **FFmpeg** : détection automatique et bouton de téléchargement (~80 Mo) lorsque la conversion, la fusion ou l'incrustation en a besoin.
- **Annulation** : annulation globale de la file ou d'un élément, pendant l'analyse ou le téléchargement.
- **Journalisation** : fichier `%LOCALAPPDATA%\MediaVacuum\logs\app.log`, rotation automatique.

### Corrections importantes

- **Menu contextuel** : le dossier cliqué est maintenant transmis via `--folder` et pré-remplit le dossier de sortie. Le menu fonctionne sur un dossier et sur l'arrière-plan d'un dossier.
- **Mise à jour yt-dlp** : l'ancienne vérification lisait l'en-tête `Location` après que `HttpClient` avait suivi la redirection, donc elle ne détectait jamais rien. Elle utilise maintenant l'API GitHub Releases, avec un repli sur la redirection et vérification SHA-256 lorsque disponible.
- **Auto-update de MediaVacuum** : au démarrage, l'application vérifie la dernière release GitHub. Si une version plus récente existe, elle la télécharge, remplace l'exécutable via un petit script de mise à jour et relance automatiquement la nouvelle version. L'option peut être désactivée dans les réglages avancés ; un bouton permet alors de lancer la mise à jour manuellement.
- **Progression en culture française** : les nombres de yt-dlp sont maintenant analysés en culture invariante (`42.5` ne dépend plus de la locale).
- **Presets de format** : `720p`, `1080p`, etc. ne sont plus passés tels quels à yt-dlp.
- **Chemin de yt-dlp** : le téléchargement se fait dans `%LOCALAPPDATA%\MediaVacuum\yt-dlp.exe`, plus dans le répertoire courant.
- **Désinstallation** : plus de `Directory.Delete` récursif sur le dossier de l'exécutable. Seules les données connues de MediaVacuum sont supprimées, jamais les téléchargements.
- **Fichier final** : le chemin affiché utilise la sortie `after_move` de yt-dlp (fichier fusionné final) et non plus le flux vidéo intermédiaire.
- **ETA** : le temps restant est correctement lu depuis le template de progression.
- **Traductions** : catalogue complet et embarqué dans l'exécutable. Les anciens fichiers copiés dans `%LOCALAPPDATA%` sont ignorés sauf s'ils contiennent `"_overrides": "true"`.
- **Version** : la version de l'assembly n'est plus forcée à `1.0.0.0` ; `Directory.Build.props` fait foi (0.2.0).

### Technique

- Core restructuré : `YtDlpArgumentBuilder`, `ProgressParser`, `FilenameService`, `FormatSelector`, `SizeEstimator`, `DownloadQueueService`, `FfmpegService`, `UpdateService`, `LocalizationService`.
- `ProcessStartInfo.ArgumentList` remplace la concaténation manuelle des arguments (URLs et chemins avec caractères spéciaux).
- `IYtDlpService`, `IUpdateService`, `IFfmpegService`, `IContextMenuService`, `IInstallerService` et `IDialogService` pour l'injection et les tests.
- 55 tests unitaires (noms de fichiers, sélecteurs de format, parsing de progression, playlists, traductions, configuration, versions, auto-update).
- Thèmes séparés : `Light.xaml`, `Dark.xaml`, `Common.xaml`.
- Publication single-file : traductions et logo embarqués en ressources.
- Workflow CI GitHub Actions.
- Licence MIT ajoutée.

### Limites connues

- VLC affiche les pochettes intégrées des fichiers **audio**, mais pas celles des fichiers **vidéo**. La miniature est bien présente dans le fichier ; c'est une limitation de VLC.
- Sur Windows 11, le menu contextuel classique peut apparaître sous **Afficher plus d'options**.
- L'analyse et l'estimation de taille d'une très grande chaîne peuvent prendre du temps.
- Les sites nécessitant des cookies ou une authentification ne sont pas encore pris en charge.

### Migration depuis v0.1.x

- Le dossier de sortie et la langue sont conservés.
- Les nouvelles options prennent leurs valeurs par défaut.
- L'ancien réglage `SelectedFormat` n'est plus utilisé : les presets valides sont sélectionnés dans l'interface.
- L'ancien fichier `%LOCALAPPDATA%\MediaVacuum\Translations\*.json` est ignoré (sauf marqueur `_overrides`).
