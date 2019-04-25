﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarController : MonoBehaviour
{
    public enum states { moving = 1, turn, getPassenger, decide, stoped, parked };
    public enum signs {none, forward, turnRight, turnLeft, max20, min30, d, p, np };

    // Car dynamics variables

    public WheelCollider w_frontDriver;
    public WheelCollider w_frontPassenger;
    public WheelCollider w_backDriver;
    public WheelCollider w_backPassenger;

    public Transform t_frontDriver;
    public Transform t_frontPassenger;
    public Transform t_backDriver;
    public Transform t_backPassenger;

    // Drive variables
    private float horizontalInput;
    private float verticalInput;
    private bool handBrake;
    private float steeringAngle;
    public float maxSteeringAngle = 30.0f;
    public float torque = 50.0f;
    private float currentSpeed;
    public float maxSpeed;

    // Autonomous variables
    public bool autonomousMode = false;
    private GameObject sensors;
    private RadarSensorController radars;

    public bool crossProcess = false;
    public int carState = (int)states.stoped;
    // Turn Variables
    public float turnTimer = 0.0f;
    public bool willTurn = false;
    public bool turnComplete = false;
    public int turnDirection = (int)signs.none;
    // Passenger Variables
    public float passengerTimer = 0.0f;
    public bool stopForPassenger = false;
    public bool gotPassenger = false;

        // Traffic Light variables (true = green, false = red)
    public bool lightStatus = true;
    public float lightDistance;

        // Park Variables
    
    
    public void Start()
    {
        sensors = GameObject.Find("Sensors");
        radars = GameObject.Find("Radars").GetComponent<RadarSensorController>();
    }

    public void UpdateWheelPose (WheelCollider _wCollider, Transform _wTransform)
    {
        Vector3 _pos = _wTransform.position;
        Quaternion _quat = _wTransform.rotation;

        _wCollider.GetWorldPose(out _pos, out _quat);

        _wTransform.position = _pos;
        _wTransform.rotation = _quat;
    }

    private void FixedUpdate()
    {
        if (autonomousMode != true)
            Drive();
        else
            AutonomousDrive();
    }

    public void Drive()
    {
        // Get the input from the user such as left, right, gas, brake and hand brake.
        // Input.GetAxis returns a float value between 0 and 1.0f, Input.GetKey returns a bool.
        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");
        handBrake = Input.GetKey(KeyCode.Space);
        currentSpeed = 2 * Mathf.PI * w_frontDriver.radius * w_frontDriver.rpm * 60 / 1000;

        // Set the current steering angle with respect to max angle and user input.
        // Then update the WheelColliders' steering angles.
        steeringAngle = maxSteeringAngle * horizontalInput;
        w_frontDriver.steerAngle = steeringAngle;
        w_frontPassenger.steerAngle = steeringAngle;

        // If hand brake is not applied, apply the torque to the WheelColliders, else apply brake torque.
        if (handBrake != true)
        {
            if (verticalInput != 0 && currentSpeed < maxSpeed)
            {

                if (currentSpeed > 0 && verticalInput < 0)
                {
                    CarBrake();
                }
                else
                {
                    w_frontDriver.motorTorque = torque * verticalInput;
                    w_frontPassenger.motorTorque = torque * verticalInput;
                    w_frontDriver.brakeTorque = 0;
                    w_frontPassenger.brakeTorque = 0;
                    w_backDriver.brakeTorque = 0;
                    w_backPassenger.brakeTorque = 0;
                }
            }
            else
            {
                CarBrake();
            }
        }
        else
        {

            w_frontDriver.motorTorque = 0;
            w_frontPassenger.motorTorque = 0;
            w_frontDriver.brakeTorque = torque * 1000;
            w_frontPassenger.brakeTorque = torque * 1000;
            w_backDriver.brakeTorque = torque * 1000;
            w_backPassenger.brakeTorque = torque * 1000;
        }

        // Update all the wheel positions according to the WheelColliders' positions.
        UpdateWheelPose(w_frontDriver, t_frontDriver);
        UpdateWheelPose(w_frontPassenger, t_frontPassenger);
        UpdateWheelPose(w_backDriver, t_backDriver);
        UpdateWheelPose(w_backPassenger, t_backPassenger);

        // Calculate the Km/h.
        // WARNING! -THIS APPROACH MIGH BE WRONG AS WE DO NOT KNOW THE WHEEL RADIUS' UNIT!
        // IT IS ASSUMED TO BE IN MILLIMETER(MM) THEN CONVERTED TO CANTIMETER(CM) IN THE CALCULATION!.
        //Debug.Log("Km/h: " + (w_backDriver.rpm * w_backDriver.radius * 100 * 0.001885));
        //Debug.Log("RPM: " + w_backDriver.rpm);
        //Debug.Log("Steering Angle: " + steeringAngle);
    }

    public void AutonomousDrive()
    {
        
        currentSpeed = 2 * Mathf.PI * w_frontDriver.radius * w_frontDriver.rpm * 60 / 1000;

        if(carState == (int)states.stoped)
        {
            if (radars.isHitFront == true && radars.hitFront.distance < 1.0f)
                carState = (int)states.stoped;
            else
                carState = (int)states.moving;
        }
        /* SEQUENCE
         * 1- Check front left & right radars' distances, if >15, stop. There must be a turn.
         *  1.1- Check light if there is one - else canMove = true.
         *  1.2- Check turn sign if there is one - else direction = forward.
         *  1.3- Set turn and perform turn action.
         * 2- if < 15, try to centrize.
         * 3- Check for signs
         *  3.1- If there is any, perform sign action.
         * REPEAT
         **/
        
        if(carState != (int)states.decide)
        {
            checkLight();
            crossRoadCheck();
        }

        switch (carState)
        {
            case (int)states.moving:
                steerCalculation();
                if (radars.isHitLeftFront && radars.isHitRightFront)
                {
                    verticalInput = 0.3f;
                    
                    if (verticalInput != 0 && currentSpeed < maxSpeed)
                    {

                        if (currentSpeed > 0 && verticalInput < 0)
                        {
                            CarBrake();
                        }
                        else
                        {
                            CarDrive();
                        }
                    }
                    else
                    {
                        CarBrake();
                    }
                }

                break;
            case (int)states.turn:
                Debug.Log("Turn! " + (signs)turnDirection);
                if (turnDirection == (int)signs.forward && turnTimer < 4.0f)
                {
                    verticalInput = 0.3f;
                    horizontalInput = 0.0f;
                    turnTimer += Time.deltaTime;
                    CarDrive();
                }
                else if (turnDirection == (int)signs.turnRight && turnTimer < 4.0f)
                {
                    Debug.Log("Turn Right");
                }
                else if (turnDirection == (int)signs.turnLeft && turnTimer < 4.0f)
                {
                    Debug.Log("Turn Left");
                }
                else
                {
                    carState = (int)states.moving;
                    turnComplete = true;
                    crossProcess = false;
                    turnTimer = 0.0f;
                }
                break;
            case (int)states.getPassenger:
                break;
            case (int)states.decide:
                if (willTurn)
                {
                    turnComplete = false;
                    willTurn = false;
                    carState = (int)states.turn;
                }//ELSI KALDIR IMPELEMTASYONU YAPINCA GEREK YOK HERALDE?
                else if(stopForPassenger)
                {
                    carState = (int)states.getPassenger;
                }
                    
                break;
            case (int)states.parked:
                CarBrake();
                break;
            case (int)states.stoped:
                CarBrake();
                break;
            default:
                break;
        }

       
    }

    private void crossRoadCheck()
    {
        if (radars.hitRightFront.distance > 10.0f && radars.hitLeftFront.distance > 10.0f && crossProcess == false)
        {
            // Crossing change state to decide!
            carState = (int)states.decide;
            crossProcess = true;
            Debug.Log("Cross Road Detected!");
            verticalInput = 0.0f;
            horizontalInput = 0.0f;
            CarBrake();
        }
    }

    private void steerCalculation()
    {
        if (radars.hitLeftFront.distance > (radars.hitRightFront.distance + 0.3f))
        {
            //turn left
            //Debug.Log("LEFT!");
            if (horizontalInput < -1)
                horizontalInput = -1.0f;
            else
                horizontalInput -= 0.1f;
        }
        else if (radars.hitRightFront.distance > (radars.hitLeftFront.distance + 0.3f))
        {
            //turn right
            //Debug.Log("RIGHT!");
            if (horizontalInput > 1)
                horizontalInput = 1.0f;
            else
                horizontalInput += 0.1f;
        }
        else
        {
            //go straight
            horizontalInput = 0;
        }
    }

    private void steerCalculationForPassenger()
    {
        if (radars.hitLeftFront.distance > (radars.hitRightFront.distance + 0.3f))
        {
            //turn left
            //Debug.Log("LEFT!");
            if (horizontalInput < -1)
                horizontalInput = -1.0f;
            else
                horizontalInput -= 0.1f;
        }
        else if (radars.hitRightFront.distance > (radars.hitLeftFront.distance + 0.3f))
        {
            //turn right
            //Debug.Log("RIGHT!");
            if (horizontalInput > 1)
                horizontalInput = 1.0f;
            else
                horizontalInput += 0.1f;
        }
        else
        {
            //go straight
            horizontalInput = 0;
        }
    }

    private void checkLight()
    {
        if (lightStatus == false && lightDistance < 15.0f)
            carState = (int)states.stoped;
    }

    public void CarBrake()
    {
        w_frontDriver.motorTorque = 0;
        w_frontPassenger.motorTorque = 0;
        w_frontDriver.brakeTorque = torque;
        w_frontPassenger.brakeTorque = torque;
        w_backDriver.brakeTorque = torque;
        w_backPassenger.brakeTorque = torque;
    }

    public void CarDrive()
    {
        steeringAngle = maxSteeringAngle * horizontalInput;
        w_frontDriver.steerAngle = steeringAngle;
        w_frontPassenger.steerAngle = steeringAngle;
        w_frontDriver.motorTorque = torque * verticalInput;
        w_frontPassenger.motorTorque = torque * verticalInput;
        w_frontDriver.brakeTorque = 0;
        w_frontPassenger.brakeTorque = 0;
        w_backDriver.brakeTorque = 0;
        w_backPassenger.brakeTorque = 0;
    }

    public float getSteeringAngle()
    {
        return steeringAngle;
    }

    public float getSpeed()
    {
        return currentSpeed;
    }
}       
