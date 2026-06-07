using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InfiniteGround : MonoBehaviour
{
    public SpriteRenderer _ground1, _ground2;
    public float _moveSpeed=1;
    private SpriteRenderer _nextGround,_currentGround,_tempGround;
    private float _groundLength;
    
    void Start()
    {
        _currentGround=_ground1;
        _nextGround = _ground2;
        _groundLength=_ground1.sprite.bounds.size.x;
    }

    // Update is called once per frame
    void Update()
    {
        Move(_moveSpeed);
        UpdateGround();
    }

    public void Move(float dis)
    {
        _ground1.transform.position -=new Vector3(dis*Time.deltaTime,0,0);
        _ground2.transform.position -=new Vector3(dis*Time.deltaTime,0,0);
    }

    void UpdateGround()
    {
        if (_currentGround.transform.position.x < 0)
        {
            _nextGround.transform.position = _currentGround.transform.position + new Vector3(_groundLength,0,0);
            
            _tempGround = _currentGround;
            _currentGround = _nextGround;
            _nextGround = _tempGround;
        } 
    }
}
