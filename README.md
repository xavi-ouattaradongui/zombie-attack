# 🧟 Zombie Attack

**Cours, esquive et survis à la horde !**
Zombie Attack est un jeu de course infinie en 3D, dans l'esprit de Subway Surfers. Une horde de zombies est à tes trousses dans une ville abandonnée.

### ▶️ [Jouer dans le navigateur](https://xavi-ouattaradongui.github.io/zombie-attack/)

Le jeu marche sur téléphone (tactile) et sur ordinateur (clavier).

---

## 🎮 Commandes

| Action | Téléphone | Ordinateur |
|---|---|---|
| Changer de couloir | Glisser à gauche / droite | ← → |
| Sauter | Glisser vers le haut | ↑ |
| Glisser sous un obstacle | Glisser vers le bas | ↓ |
| Pause | Bouton II | Échap / P |

## ✨ Ce qu'il y a dans le jeu

- **Course infinie sur 3 couloirs** avec des obstacles, des voitures et des zombies qui foncent vers toi. On peut aussi sauter sur les petites voitures et courir dessus.
- **4 quartiers** qui changent tous les 1 300 m : le centre-ville, le Quartier des Saules, la zone industrielle nord et le Port Est.
- **Le Colosse**, un zombie géant qui arrive à chaque nouveau quartier. Il frappe le sol et envoie des ondes toxiques qu'il faut esquiver.
- **4 super objets** : l'aimant, le bouclier, le super saut et les pièces x2.
- **Une boutique** pour que les super objets durent plus longtemps (5 niveaux chacun).
- **Une seconde chance** qui coûte 25 pièces, puis 50, 100, 200…
- **Des missions** qui rapportent des pièces, des paramètres (volume, sensibilité) et la pause automatique.

## 🛠️ Technique

- **Moteur :** Unity 6 (6000.3 LTS), Universal Render Pipeline, en C#.
- **Code :** l'interface est entièrement construite en code (uGUI). La scène est générée par un outil d'éditeur (menu *Zombie Run → Créer la scène*).
- **Version web :** WebGL, publiée sur GitHub Pages depuis le dossier `docs/`.

## 🚀 Mettre à jour la version en ligne

1. Dans Unity, ouvre le menu **Zombie Run → Construire la version web (dossier docs)**.
2. Dans le Terminal, depuis le dossier du projet :
   ```bash
   git add .
   git commit -m "Nouvelle version"
   git push
   ```
3. Le site se met à jour tout seul en 1 à 2 minutes.

> Les personnages Mixamo et la musique de course ne sont pas dans ce dépôt, à cause de leurs licences. Pour reconstruire le projet, il faut les remettre dans `Assets/ZombieRun/Characters` et `Assets/ZombieRun/Audio`.

## 🙏 Crédits

- **Personnages et animations :** [Mixamo](https://www.mixamo.com) (Adobe)
- **Décors 3D :** [Kenney](https://kenney.nl), sous licence CC0
- **Musique de course :** [Pixabay](https://pixabay.com)
- **Polices :** Bangers et Lilita One ([Google Fonts](https://fonts.google.com), sous licence SIL OFL)

---

Jeu réalisé par **Dongui Ouattara**.
