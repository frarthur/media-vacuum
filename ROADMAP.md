# MediaVacuum — Roadmap

## Vision

Application Windows native qui rend **yt-dlp** confortable : file de téléchargement visuelle, playlists et chaînes, miniatures, tailles, formats, thèmes clair/sombre et menu contextuel dans l'Explorateur.

---

## État actuel — v0.2.1

### Fondations
- [x] Solution .NET 9 / WPF / MVVM en 3 projets (UI, Core, Installer)
- [x] Core sans dépendance WPF
- [x] Configuration persistante versionnée
- [x] Journalisation locale et gestionnaire d'exceptions
- [x] Tests unitaires (modèles, parsing, traductions, file)

### Téléchargement
- [x] Analyse d'une URL avant téléchargement
- [x] File de téléchargement avec sélection, suppression et annulation
- [x] Playlists et chaînes : une ligne par vidéo
- [x] Miniatures, durées et tailles estimées
- [x] Progression par élément (pourcentage, vitesse, temps restant)
- [x] Progression globale et taille totale de la sélection
- [x] Téléchargements simultanés configurables
- [x] Noms de fichiers propres et dossiers par playlist
- [x] Formats vidéo valides (`bv*[height<=…]+ba/b[…]`)
- [x] Conteneurs MP4/MKV/WebM et réencodage optionnel
- [x] Extraction audio (MP3, M4A, AAC, FLAC, Opus, Vorbis, WAV, ALAC)
- [x] Sélecteur de format avancé
- [x] Métadonnées, miniatures et sous-titres

### Outils
- [x] Détection de ffmpeg et téléchargement guidé
- [x] Détection des runtimes JavaScript (Deno/Node/Bun/QuickJS) et téléchargement de Deno
- [x] Mise à jour automatique de yt-dlp au démarrage
- [x] Vérification des mises à jour yt-dlp via l'API GitHub
- [x] Vérification SHA-256 lorsqu'elle est disponible
- [x] Mise à jour automatique de MediaVacuum (téléchargement, remplacement de l'exe, relance)
- [x] Menu contextuel HKCU (dossier et arrière-plan de dossier)
- [x] Thèmes Système / Clair / Sombre
- [x] 5 langues complètes
- [x] Désinstallation sûre (jamais les téléchargements)

### Distribution
- [x] Publication single-file win-x64
- [x] Licence MIT
- [x] Documentation utilisateur
- [x] Workflow CI GitHub Actions

---

## Prochaines étapes possibles

### v0.3
- [ ] Historique persistant des téléchargements
- [ ] Reprise d'une file après redémarrage
- [ ] Cookies / authentification (navigateur ou fichier)
- [ ] Sous-titres : téléchargement séparé ou incrustation au choix par langue
- [ ] Aperçu du média (titre, chaîne, miniature, formats disponibles)
- [ ] Vitesse limite par élément

### v0.4
- [ ] File de plusieurs URLs (coller plusieurs liens d'un coup)
- [ ] Modèles de noms personnalisables
- [ ] Presets de téléchargement (vidéo, audio, archive)
- [ ] SponsorBlock et chapitres

---

## Règles de commit

Format défini dans `.gitmessage` :

```
<type>(<scope>): <sujet>
```

Types : `feat` | `fix` | `refactor` | `style` | `test` | `docs` | `chore` | `perf`

---

## Workflow de release

```powershell
# 1. Mettre à jour la version dans Directory.Build.props
# 2. Construire l'exécutable et l'archive
.\publish.ps1

# 3. Créer la release GitHub
gh release create v0.2.1 --title "v0.2.1" --notes-file RELEASE_NOTES.md .\MediaVacuum-0.2.1-win-x64.zip
```

---

## Crédits

- [yt-dlp](https://github.com/yt-dlp/yt-dlp) — moteur de téléchargement
- [FFmpeg](https://ffmpeg.org/) — conversion et post-traitement
