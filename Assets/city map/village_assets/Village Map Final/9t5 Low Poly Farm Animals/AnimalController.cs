using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimalController : MonoBehaviour
{
    public float Speed;
    public float RSpeed = 0.05f;
    // Start is called before the first frame update
    public int State; //0 = eating //1 = walking
    public float ChangeStateTime;
    public Vector2 NextPoint;
    public float MovementRangeMax;
    public float MovementRangeMin;

    public int NegativeornotX;
    public int NegativeornotY;
    public Vector3 initialpos;
    public Vector3 currentpos;
    Vector3 TargetRotation;
    public Animator Anim;
    void Start()
    {
        initialpos = transform.position;
        Anim = GetComponent<Animator>();
        ChangeState();
    }

    // Update is called once per frame
    void Update()
    {
        
        if (State == 1)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(TargetRotation), Time.time * 0.05f);
            transform.Translate(0,0,1*Speed*Time.deltaTime);
            if (NegativeornotX == 0)
            {
                if (transform.position.x >= NextPoint.x)
                {
                    ChangeState();
                }
            }
            else if (NegativeornotX == 1)
            {
                if (transform.position.x <= NextPoint.x)
                {
                    ChangeState();
                }
            }
            if (NegativeornotY == 0)
            {
                if (transform.position.z >= NextPoint.y)
                {
                    ChangeState();
                }
            }
            else if (NegativeornotY == 1)
            {
                if (transform.position.z <= NextPoint.y)
                {
                    ChangeState();
                }
            }

        }
    }
    public void ChangeState()
    {
        State = Random.Range(0, 2);
        Anim.SetInteger("NROANIM", State);
        if (State == 0)
        {
            //EAT
            float TimeState = Random.Range(ChangeStateTime / 2, ChangeStateTime);
            Invoke("ChangeState", TimeState);

        }
        else if (State == 1)
        {
            //WALK
            float ranx = Random.Range(MovementRangeMin, MovementRangeMax);
            NegativeornotX = Random.Range(0,2);
            if(NegativeornotX == 1) {
                ranx *= -1;
            }
            float ranz = Random.Range(MovementRangeMin , MovementRangeMax);
             NegativeornotY = Random.Range(0, 2);
            if (NegativeornotY == 1)
            {
                ranz *= -1;
            }

            NextPoint.x = ranx + initialpos.x;
            NextPoint.y = ranz + initialpos.z;
            currentpos = transform.position;
             TargetRotation = new Vector3(NextPoint.x - transform.position.x, 0, NextPoint.y - transform.position.z);

        }
       
    }
}
