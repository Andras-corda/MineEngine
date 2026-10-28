Décomposé en émitteurs qui possèdent des widgets :

composé d'une tête (appelé Head) => contient à gauche un checkbox qui défini si il est rendu en preview, au milieu le nom de l'émitter (la taille en x s'allonge en fonction de la taille du nom pour garder une taille adaptive)

composé d'un corps (appelé body) => contient des partie (comme des dossier), c'est parties possèdent les éléments de personalisation :

1. Properties 
    a. Performance : contient les ressources prisent sur le cpu/gpu et un récapitulatif
2. Emitter spawn 
    a. Lifecycle : Contient le life cycle de la particle
    b. ID : contient un id numérique qui permet de l'identifier
3. Particle spawn
    a. Spawn particle : Contient les infos relatif au spawn de la particule comme ses coordonées, sa durée de vie unitaire, le nombre de particule en tout
    b. Velocity : Contient les infos qui donne la vélocité et la direction à la particule
4. Particle Update
    a. Gravity : applique ou nom une gravité sur les particules (vecteur 3)
    b. Collision : applique une collision sur les particules
    c. Scale : change le scale des particules (peut être une valeur fixe, dynamique celon le temps ou aléatatoire celon une range)
5. Render
    a. Texture : permet de mettre la texture de la particule
6. Event
    a. Generate death event : génère un event celon la mort de la particule (permet de passer à la particule qui reçois l'event les coordonées relatives de la particules)
    b. Receive death event : reçoit l'event et déclenche le spawn de la particule celon l'event + permet de oui ou non instancier la nouvelle particule à l'endroit de la mort de la 1ere (les emitters doivent avoir un id pour envoyer et recevoir un event)