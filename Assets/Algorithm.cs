﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Algorithm : MonoBehaviour
{
    Movement movement;
    Sensors sensors;
    public GridMap grid;
    public WallGenerator wallGen;
    float xmin, ymin, xmax, ymax; 


       
    // The width and height in unity units of one pixel in the grid
    public float gridPixelWidth, gridPixelHeight;

    // A reference to the goal so we can get its position to calibrate the grid
    public Goal goal;

    // The position we believe we are on the grid
    public Vector2Int gridPos;

    // The number of frames left we need to turn for
    public float turnTimer = 0;

    // The amount we think we're rotated
    public float assumedRotation = 0;

    // The maximum length a sensor can read before we consider it a wall
    public float maxLengthToWall;

    // Variable to mark that we're turning and shouldn't do anything until it's done
    public bool turning = false;

    // Variable to mark that we're moving and shouldn't be doing any logic
    public bool moving = false;

    // The number and direction of turns to make
    public int turnsToMake = 0;

    // Which way we're facing
    public Vector2Int facing = Vector2Int.up;

    public bool running = true;

    // Where the goal is on the grid
    private Vector2Int goalPos;

    // The amount we have left to move in world space units
    public float amountToMove = 0;

    // The size of the robot in pixels according to the grid
    public Vector2Int robotSize;

    private Vector2 initalPosition;
    private List<Vector2Int> nextInstructions = new List<Vector2Int>();

    // Start is called before the first frame update
    void Start()
    {
        movement = GetComponent<Movement>();
        sensors = GetComponent<Sensors>();

        //xmin = wallGen.xmin;
        xmin = -5;
        ymin = -5;
        //xmax = wallGen.xmax;
        xmax = 5;
        ymax = 5;

        // Hardcoded to 5% error allowance
        maxLengthToWall = sensors.maxDist - sensors.maxDist * 0.05f;

        initalPosition = transform.position;

        gridPixelWidth = (xmax - xmin) / grid.width;
        gridPixelHeight = (ymax - ymin) / grid.height;

        int y     = Mathf.RoundToInt((ymax - transform.position.y) / gridPixelHeight);
        int goaly = Mathf.RoundToInt((ymax - goal.transform.position.y) / gridPixelHeight);

        gridPos = new Vector2Int(grid.width / 2, y);
        goalPos = new Vector2Int(grid.width / 2, goaly);

        grid.SetPixel(goalPos.x, goalPos.y, GridMap.PixelStates.GOAL);
        grid.SetPixel(gridPos.x, gridPos.y, GridMap.PixelStates.ROBOT);

        // We round up to make sure we don't hit anything
        robotSize = new Vector2Int((int)Mathf.Ceil(GetComponent<CircleCollider2D>().radius / gridPixelWidth), (int)Mathf.Ceil(GetComponent<CircleCollider2D>().radius / gridPixelHeight));
        robotSize.x += 4;
        robotSize.y += 4;

        GetComponent<BoxCollider2D>().size = new Vector2(robotSize.x * gridPixelWidth, robotSize.y * gridPixelHeight);
    }

    void DrawGrid(float[] readings) {
       
        for (int i = 0; i < readings.Length; i++)
        {
            // angle determines the angle we believe the sensor to be at
            float angle = (2 * Mathf.PI / 16) * i + Mathf.Deg2Rad * assumedRotation;
            int x1 = Mathf.RoundToInt(gridPos.x);
            int y1 = Mathf.RoundToInt(gridPos.y);
            int x2 = Mathf.RoundToInt(x1 + Mathf.Cos(angle) * (readings[i] / gridPixelWidth));
            int y2 = Mathf.RoundToInt(y1 - Mathf.Sin(angle) * (readings[i] / gridPixelHeight));

            List<Vector2Int> points = GridMap.bresenham(x1, y1, x2, y2);
            Vector2Int lastPoint = points[points.Count - 1];

            foreach (Vector2Int point in points)
            {
                grid.SetPixel(point.x, point.y, GridMap.PixelStates.EMPTY);
            }

            // If the sensor reads further than the max distance we consider the end to be empty
            if(readings[i] > maxLengthToWall)
                grid.SetPixel(lastPoint.x, lastPoint.y, GridMap.PixelStates.EMPTY);
            // If the sensor reads closer than the max distance we consider it a wall
            // this is because the max dist should be set lower than the error could produce on the robot
            else
                grid.SetPixel(lastPoint.x, lastPoint.y, GridMap.PixelStates.WALL);
        }

    }

    void MoveForward() {
        
        // If the amount we need to move is greater than the max we can move in one frame
        // then we just move at full speed for this frame and subtract the amount we just moved from the amount we have left to do
        if( amountToMove > movement.speed)
        {
            movement.PutMovement(1, 0);
            amountToMove -= movement.speed;
        }
        // Otherwise we need to calculate how much power to put into the wheels
        else
        {
            float forwardPower = amountToMove / movement.speed;

            // Sanity check that forwardPower is less than 1
            if (forwardPower > 1)
                throw new System.Exception("Forward velocity shouldn't be greater than 1");

            movement.PutMovement(forwardPower, 0);
            // We have to invert y here because the grid is inverted from the world space
            gridPos = new Vector2Int(gridPos.x + facing.x, gridPos.y - facing.y);
            moving = false;

        }
    }

    void Rotate() {
        
        // Turning left 
        if (turnsToMake > 0)
        {
            float amountRotated;
            if(turnTimer > 1)
            {
                movement.PutMovement(0, 1);
                amountRotated = 1;
            }
            else
            {
                movement.PutMovement(0, turnTimer);
                amountRotated = turnTimer;
            }
            assumedRotation += movement.maxAngleDelta * amountRotated;
            turnTimer -= amountRotated;
            if (turnTimer == 0)
            {
                turning = false;
                turnsToMake--;
                facing = RotateVector(facing, 1);
            }
        }
        // Turning right
        else if (turnsToMake < 0)
        {
            float amountRotated;
            if (turnTimer > 1)
            {
                movement.PutMovement(0, -1);
                amountRotated = 1;
            }
            else
            {
                movement.PutMovement(0, -turnTimer);
                amountRotated = turnTimer;
            }
            assumedRotation -= movement.maxAngleDelta * amountRotated;
            turnTimer -= amountRotated;
            if (turnTimer == 0)
            {
                turning = false;
                turnsToMake++;
                facing = RotateVector(facing, -1);
            }
        }
        else {
            throw new System.Exception("You shouldn't call rotate with turnsToMake = 0");
        }
        
    }

    public void Restart()
    {

        transform.position = initalPosition;
        transform.rotation = Quaternion.identity;

        int y = Mathf.RoundToInt((ymax - transform.position.y) / gridPixelHeight);

        gridPos = new Vector2Int(grid.width / 2, y);

        turnTimer = 0;
        assumedRotation = 0;
        turning = false;
        moving = false;
        turnsToMake = 0;
        facing = Vector2Int.up;
        running = true;
        amountToMove = 0;

        grid.Restart();

   
}

    private Vector2Int RotateVector(Vector2Int v, int direction) {
        if (direction == 1)
        {
            if (v.x == 1 && v.y == 0)
                return new Vector2Int(0, 1);
            else if (v.x == 0 && v.y == 1)
                return new Vector2Int(-1, 0);
            else if (v.x == -1 && v.y == 0)
                return new Vector2Int(0, -1);
            else if (v.x == 0 && v.y == -1)
                return new Vector2Int(1, 0);
            else
                throw new System.Exception("You can't call rotate vector with a vector that isn't of the form ([01(-1)], [01(-1)]");
        }
        else if (direction == -1)
        {
            if (v.x == 1 && v.y == 0)
                return new Vector2Int(0, -1);
            else if (v.x == 0 && v.y == 1)
                return new Vector2Int(1, 0);
            else if (v.x == -1 && v.y == 0)
                return new Vector2Int(0, 1);
            else if (v.x == 0 && v.y == -1)
                return new Vector2Int(-1, 0);
            else
                throw new System.Exception("You can't call rotate vector with a vector that isn't of the form ([01(-1)], [01(-1)]");
        }
        else {
            throw new System.Exception("You can't call rotate vector with a direction that isn't -1 or 1.");
        }
    }

    void Turn(int direction) {
        turnTimer = (Mathf.PI / 2) / (Mathf.Deg2Rad * movement.maxAngleDelta);
        turning = true;
        turnsToMake += direction;
        Rotate();
    }

    // turnTo 1 means left turnTo -1 means right.
    void FixedUpdate()
    {

        DrawGrid(sensors.GetReadings());


        //Debug.Break();
        // Handles turning

        if (turning)
        {
            Rotate();
        }
        else if (moving)
        {
            MoveForward();
        }
        // Handles logic for when we need to decide what to do
        else
        {
            if(nextInstructions.Count == 0)
            {
                List<Vector2Int> path = grid.BFS(gridPos, goalPos, robotSize);
                if(path == null || path.Count == 0)
                {
                    movement.PutMovement(0, 0);
                    running = false;
                    return;
                }
                nextInstructions.AddRange(path.Take(1));
            }

            Vector2Int nextInstruction = nextInstructions[0];
            // Rotate to the direction we need to go
            if (nextInstruction != facing)
            {
                if (RotateVector(facing, 1) == nextInstruction)
                {
                    Turn(1);
                }
                else{
                    Turn(-1);
                }
                    
            }
            else
            {
                
                // We need to move one grid pixel, so if we're facing right or left we need to go one pixel width
                if (facing == Vector2Int.left || facing == Vector2Int.right)
                    amountToMove = gridPixelWidth;
                // If we're facing up or down we need to go one pixel height
                else
                    amountToMove = gridPixelHeight;
                moving = true;
                
                MoveForward();
                nextInstructions.RemoveAt(0);
            }
        }
    }
}
