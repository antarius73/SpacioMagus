## Manuel du joueur — mécaniques de Mob le Magus

Ce manuel décrit les règles exactes du moteur, comme le ferait une notice complète. Il ne dit rien d'un niveau en particulier : ni rondes, ni contenu des contenants, ni seuils d'étoiles, ni chemin gagnant.

### Déroulement d'un tour
1. Le magus fait une action, qui coûte un tour : marcher, attendre, ouvrir une porte, actionner une manette, briser un contenant, allumer une alcôve, lancer un sort.
2. S'il est arrivé sur l'escalier de sortie, la partie est gagnée immédiatement : les monstres ne rejouent pas.
3. Les monstres agissent un par un, **du plus proche au plus éloigné du magus** (distance en cases, diagonales comprises).
4. Les portes et herses ouvertes décomptent, puis se referment.
5. Si le magus n'a plus de quoi lancer un sort et ne peut plus rien atteindre d'utile (escalier, fiole, contenant à briser), il est **coincé** : défaite.

### Distances, déplacement, vue
- **Distance** : nombre de pas en comptant les diagonales. C'est la plus grande des deux différences |dx| et |dy|. La portée des sorts se mesure ainsi (portée 5 = carré de 11 × 11 centré sur le magus).
- **Marcher** : vers une des 8 cases voisines. Interdit sur mur, vide, porte ou herse fermée, brasero, eau, manette, escalier montant, contenant ou créature. Une diagonale est interdite si l'une des deux cases de coin est un mur, du vide ou une porte fermée : on ne coupe pas un coin.
- **Ligne de vue** : une ligne droite de case en case, sans limite de distance. Elle est arrêtée par les murs, le vide et les portes ou herses fermées. Elle passe au-dessus de l'eau, des braseros, des contenants, des manettes et des créatures. Une diagonale qui passe pile entre deux cases bloquantes est arrêtée.

### Repérage et poursuite
- Un monstre non alerté **suit une ronde fixe et cyclique**. Observez-la quelques tours pour la prévoir. Dérangé (poussé, échangé), il rejoint sa ronde au plus vite.
- Au début de son action, un monstre non alerté qui **voit** le magus le **repère** et se met à le traquer. Il n'y a pas de limite de distance : seule compte la ligne de vue.
- Un monstre qui traque prend le **plus court chemin** vers le magus, à 1 case par tour. Il contourne ses zones de peur. Il ne franchit pas une porte fermée et ne l'ouvre pas : il attend derrière.
- **Blobs** : ils traquent jusqu'à perdre la trace. Cela arrive quand le magus se téléporte hors de leur vue, ou quand Spacio Creatura les étourdit. Mais s'ils le revoient à leur action suivante, ils le repèrent de nouveau.
- **Chauves-souris** : elles ne perdent **jamais** la trace une fois le magus repéré, même après une téléportation ou un échange.

### Capture et morsure
- **Capture (blobs)** : un blob qui traque ou vient de repérer le magus, et qui est **sur une case voisine** au moment d'agir (coins libres), le capture. La partie est perdue. Exception : la case du magus est dans la zone de peur de ce blob.
- Conséquence : **finir son tour à côté d'un blob éveillé qui vous voit, hors de sa zone de peur, est mortel**. Un blob à 2 pas qui traque sera voisin après son action : il faut s'éloigner au tour suivant.
- À vitesse égale (1 case par tour), un blob qui vous suit ne vous rattrape pas tant que vous avancez. Le danger vient des culs-de-sac, des portes fermées devant vous et des monstres qui arrivent de face.
- **Morsure (chauve-souris)** : une chauve-souris voisine qui traque mord. Elle prend 1 gemme puis somnole 2 tours. Mordu sans gemme, le magus s'évanouit : défaite. Elle ne capture pas.
- Les chauves-souris survolent l'eau, les contenants, les manettes et les portes ouvertes. Elles ne passent ni les murs ni les portes fermées.

### Peur et mutation
- **Zone de feu**, crainte des blobs verts : les 3 cases sous chaque torchère et les 8 cases autour de chaque brasero.
- **Zone d'eau**, crainte des blobs rouges : les cases voisines d'une case d'eau.
- Un blob qui se trouve dans sa zone de peur la quitte en priorité.
- Un blob **bloqué par la peur** perd son tour (« a peur »). Il est bloqué quand il ne peut pas s'approcher sans entrer dans sa zone de peur, ou quand il est voisin du magus mais que le magus est dans cette zone.
- S'il est encore bloqué au tour suivant, il **mute** : il reste immobile un tour de plus, puis change de couleur (vert → rouge, rouge → vert).
- Conséquence : **une zone de peur protège environ 3 tours** d'un blob qui vous serre de près. Après, il a changé de couleur et ne craint plus cette zone.

### Sorts (coût 1 gemme, 4 gemmes au maximum)
- **Spacio Magus** : téléportation vers une case libre où l'on peut marcher, à portée (5) et en vue. Pas sur un contenant, l'eau ou une créature. On peut passer au-dessus d'un monstre ou d'un obstacle tant que la vue n'est pas bloquée.
- **Spacio Creatura** : échange la place de deux créatures, toutes deux à portée et en vue. Chacune doit pouvoir tenir sur la case de l'autre : pas de blob posé sur l'eau. Les deux sont **étourdies 4 tours** : elles ne bougent pas, ne capturent pas, ne mordent pas pendant les 4 prochaines phases des monstres. Elles rejouent à la 5e. Un monstre étourdi bloque sa case. Les blobs étourdis perdent la trace.
- Les cases de téléportation et les créatures échangeables sont toujours listées par l'outil `etat`, comme le jeu les surligne.

### Ressources
- **Fiole** : marcher dessus (ou y être téléporté ou poussé) remplit toutes les gemmes. Si elles sont déjà pleines, la fiole reste en place pour plus tard.
- **Contenant** (tonneau, caisse, vase) : infranchissable. Le briser depuis une case voisine prend 1 tour. Il cache parfois une fiole, qui apparaît alors sur sa case : il faut encore y marcher.
- **Alcôve** : l'allumer depuis une case voisine prend 1 tour. Les contenants qui cachent une fiole deviennent alors visibles (`U`, `K`, `J` sur la carte).

### Portes et herses
- **Porte** : l'ouvrir depuis une case voisine prend 1 tour. Elle reste ouverte 5 tours (le tour d'ouverture ne compte pas), puis se referme.
- **Herse** : elle ne s'ouvre qu'en actionnant sa manette depuis une case voisine (1 tour, même durée). Une manette peut lever plusieurs herses. C'est impossible si une créature est posée sur la manette.
- En se refermant, une porte **repousse** qui se tient dessous, magus compris, vers un côté libre. Si personne ne peut être repoussé, elle se referme un tour plus tard.
- Une porte refermée **sépare** : aucun monstre ne la franchit. Franchir une porte et la laisser se refermer derrière soi est le meilleur moyen de semer des poursuivants.

### Étoiles
Moins de tours donne plus d'étoiles. Les seuils sont propres à chaque niveau et ne sont connus qu'à la victoire.

### Ce que le manuel ne dit pas, à découvrir en jouant
Les rondes de chaque monstre, quels contenants cachent une fiole, les mécanismes propres à un niveau et les seuils d'étoiles.
