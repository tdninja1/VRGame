using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Autohand.Demo{
public class PlayerFallCatcher : MonoBehaviour{
    public AutoHandPlayer player;
    Vector3 startPos;

    void Awake(){
        startPos = player.transform.position;
        if (SceneManager.GetActiveScene().name != "Demo")
            enabled = false;
    }
        
    void Update(){
        if(player.transform.position.y < -10f)
            player.SetPosition(startPos+Vector3.up);
    }
}
}
