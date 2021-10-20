//******************************************************************************

//                            Author: Tushar Ranjan, Sankalp Pandey
//                            Course: Distributed Operating Systems
//                            Instructor: Alin Dobra

//*******************************************************************************/

#time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"

open System
open System.Diagnostics
open Akka.Actor
open Akka.FSharp

type GossipPushSum =
    | SetNeighbours of IActorRef[]
    | StartGossip of string
    | TerminateGossip of string
    | StartPushSum of int
    | PushSum of float * float
    | TerminatePushSum of float * float
    | SetValues of int * IActorRef[] * int
    | Result of Double * Double

let rand = System.Random()
let timer = Stopwatch()

let system = ActorSystem.Create("System")

let mutable convergenceTime = 0

// Main actor mailbox responsible for starting and terminating algorithms
let boss (mailbox:Actor<_>) =
    let mutable convergedMessagesCount = 0
    let mutable convergedWorkersCount = 0
    let mutable startTime = 0
    let mutable totalWorkers =0
    let mutable allNodes:IActorRef[] = [||]
    let rec loop() = actor {
        let! message = mailbox.Receive()
        match message with
         | TerminateGossip message ->
                let endTime = System.DateTime.Now.TimeOfDay.Milliseconds
                convergedMessagesCount <- convergedMessagesCount + 1 
                if convergedMessagesCount = totalWorkers then
                    let realTime = timer.ElapsedMilliseconds
                    printfn "Gossip Convergence time : "
                    printfn "Real Convergence Time: %A ms" realTime
                    printfn "System Convergence Time: %A ms" (endTime-startTime)
                    convergenceTime <-endTime-startTime
                    Environment.Exit 0
                else
                    let newStart= rand.Next(0,allNodes.Length)
                    allNodes.[newStart] <! StartGossip("Hello")

            | TerminatePushSum (s,w) ->
                let endTime = System.DateTime.Now.TimeOfDay.Milliseconds
                convergedWorkersCount <- convergedWorkersCount + 1
                if convergedWorkersCount = totalWorkers then
                    let realTime = timer.ElapsedMilliseconds
                    printfn "PushSum Convergence time : "
                    printfn "Real Convergence Time: %A ms" realTime
                    printfn "System Convergence Time: %A ms" (endTime-startTime)
                    convergenceTime <-endTime-startTime
                    Environment.Exit 0

                else
                    let newStart=rand.Next(0,allNodes.Length)
                    allNodes.[newStart] <! PushSum(s,w)

            | SetValues (strtTime,nodesRef,totNds) ->
                startTime <-strtTime
                allNodes <- nodesRef
                totalWorkers <-totNds
            | _->()
        return! loop()
    }
    loop()

// Individual nodes' mailbox
let worker boss num (mailbox:Actor<_>) =
    let mutable neighbours: IActorRef[] = [||]
    let mutable timesGossipMessageHeard = 0
    let mutable terminateWorker = 0
    let mutable counter = 0
    let mutable oldSum= num |> float
    let mutable weight = 1.0
    let ratiolimit = 10.0**(-10.0)

    let rec loop() = actor {
        let! message = mailbox.Receive()
        match message with
        | SetNeighbours (arr:IActorRef[]) ->
            neighbours <- arr

        | StartGossip gossipMessage ->
            timesGossipMessageHeard <- timesGossipMessageHeard + 1
            if(timesGossipMessageHeard = 10)
            then
                boss <! TerminateGossip(gossipMessage)
            else
                let neighbourIndex= rand.Next(0,neighbours.Length)
                neighbours.[neighbourIndex] <! StartGossip(gossipMessage)

        |StartPushSum ind->

            let index = rand.Next(0,neighbours.Length)
            let neighbourIndex = index |> float
            neighbours.[index] <! PushSum(neighbourIndex,1.0)
         
        |PushSum (s,w)->
            let newsum = oldSum + s
            let newweight = weight + w
            let newSumWeightRatio = newsum / newweight
            let oldSumWeightRatio = oldSum / weight
            let sumWeightChanged = newSumWeightRatio - oldSumWeightRatio |> abs

            if (terminateWorker = 1) then

                let index = rand.Next(0, neighbours.Length)
                neighbours.[index] <! PushSum(s, w)
            
            else
                if sumWeightChanged > ratiolimit then
                    counter <- 0
                else 
                    counter <- counter + 1

                if  counter = 3 then
                    counter <- 0
                    terminateWorker <- 1
                    boss <! TerminatePushSum(oldSum, weight)
            
                oldSum <- newsum / 2.0
                weight <- newweight / 2.0
                let index = rand.Next(0, neighbours.Length)
                neighbours.[index] <! PushSum(oldSum, weight)
           
        | _-> ()
        return! loop()
    }
    loop()

// Function to start each algorithm
let startAlgo algo num nodeArr=
    (nodeArr : _ array)|>ignore
    if algo="gossip" then
        let starter= rand.Next(0,num-1)
        nodeArr.[starter]<!StartGossip("LetsGo")
    elif algo="pushsum" then
        let starter= rand.Next(0,num-1)
        nodeArr.[starter]<!StartPushSum(starter)
    else
        printfn "Wrong Algo name entered!"

//------------Topologies start-----------//

// Full Network Toplogy. All nodes are neighbours to each other
let createFullNetwork tworkers algo =
    
    let bossActor = spawn system "boss_actor" boss
    let nodes = Array.zeroCreate(tworkers)
    let mutable neighbours: IActorRef[]= [||]
    for start in [0 .. tworkers-1] do
        nodes.[start]<- worker bossActor (start+1)|> spawn system ("Actor"+string(start))
   
    for start in [0 .. tworkers-1] do
        if(start=0) then 
                neighbours <- nodes.[1..tworkers-1]
                nodes.[start]<!SetNeighbours(neighbours)
        else if(start=tworkers-1)then 
                neighbours <- nodes.[1..tworkers-2]
                nodes.[start]<!SetNeighbours(neighbours)
        else
            neighbours <- Array.append nodes.[0..start-1] nodes.[start+1..tworkers-1]
            nodes.[start] <! SetNeighbours(neighbours)
    timer.Start()
    bossActor<!SetValues(System.DateTime.Now.TimeOfDay.Milliseconds,nodes,tworkers)
    startAlgo algo tworkers nodes

// 3D Network Toplogy. Left, right, top, bottom, front and back are neighbours
let create3DNetwork tworkers algo =
    let bossActor= boss |> spawn system "boss_actor"
    let edgelength=int(floor ((float tworkers) ** (1.0/3.0)))
    printfn "edge - %i" edgelength
    let total3Dworkers=int(edgelength*edgelength*edgelength)
    let nodes = Array.zeroCreate(total3Dworkers)
    for i in [0..total3Dworkers-1] do
        nodes.[i]<- worker bossActor (i+1) |> spawn system ("Actor"+string(i))
    let mutable neighbours: IActorRef [] = [||]
    for l in [0..total3Dworkers-1] do
        if(l-1 >= 0) then neighbours <- (Array.append neighbours [|nodes.[l-1]|])
        if(l+1 < total3Dworkers) then neighbours <- (Array.append neighbours [|nodes.[l+1]|])
        if(l-edgelength >= 0) then neighbours <- (Array.append neighbours [|nodes.[l-edgelength]|])
        if(l+edgelength < total3Dworkers) then neighbours <- (Array.append neighbours [|nodes.[l+edgelength]|])
        if(l-(edgelength*edgelength) >= 0) then neighbours <- (Array.append neighbours [|nodes.[l-(edgelength*edgelength)]|])
        if(l+(edgelength*edgelength) < total3Dworkers) then neighbours <- (Array.append neighbours [|nodes.[l+(edgelength*edgelength)]|])
        nodes.[l] <! SetNeighbours(neighbours)
    timer.Start()
    bossActor<!SetValues(System.DateTime.Now.TimeOfDay.Milliseconds,nodes,total3Dworkers)
    startAlgo algo total3Dworkers nodes

// Line Topology. Left and right are neighbours
let createLineNetwork tworkers algo =
    let bossActor = spawn system "boss_actor" boss
    let nodes = Array.zeroCreate(tworkers)
    for i in [0..tworkers-1] do
        nodes.[i]<- worker bossActor (i+1) |> spawn system ("Actor"+string(i))
    let mutable neighbours:IActorRef[]=Array.empty
    for i in [0..tworkers-1] do
        if i=0 then
            neighbours<-nodes.[1..1]
            nodes.[i]<!SetNeighbours(neighbours)
        elif i=(tworkers-1) then
            neighbours<-nodes.[(tworkers-2)..(tworkers-2)]
            nodes.[i]<!SetNeighbours(neighbours)
        else
            neighbours<-Array.append nodes.[i-1..i-1] nodes.[i+1..i+1]
            nodes.[i]<!SetNeighbours(neighbours)
    timer.Start()
    bossActor<!SetValues(System.DateTime.Now.TimeOfDay.Milliseconds,nodes,tworkers)
    startAlgo algo tworkers nodes

// Imperfect 3D topology. Same as 3D but just one extra random node is neighbour
let createImp3DNetwork tworkers algo =
    let bossActor = spawn system "boss_actor" boss
    let edgelength=int(round ((float 64) ** (1.0/3.0)))
    let total3Dworkers=int(edgelength*edgelength*edgelength)
    let nodes = Array.zeroCreate(total3Dworkers)
    for i in [0..total3Dworkers-1] do
        nodes.[i]<- worker bossActor (i+1) |> spawn system ("Actor"+string(i))
    let mutable neighbours: IActorRef [] = [||]
    for l in [0..total3Dworkers-1] do
        if(l-1 >= 0) then neighbours <- (Array.append neighbours [|nodes.[l-1]|])
        if(l+1 < total3Dworkers) then neighbours <- (Array.append neighbours [|nodes.[l+1]|])
        if(l-edgelength >= 0) then neighbours <- (Array.append neighbours [|nodes.[l-edgelength]|])
        if(l+edgelength < total3Dworkers) then neighbours <- (Array.append neighbours [|nodes.[l+edgelength]|])
        if(l-(edgelength*edgelength) >= 0) then neighbours <- (Array.append neighbours [|nodes.[l-(edgelength*edgelength)]|])
        if(l+(edgelength*edgelength) < total3Dworkers) then neighbours <- (Array.append neighbours [|nodes.[l+(edgelength*edgelength)]|])
        let rnd = rand.Next(0, total3Dworkers-1)
        neighbours <- (Array.append neighbours [|nodes.[rnd] |])
        nodes.[l] <! SetNeighbours(neighbours)
    timer.Start()
    bossActor<!SetValues(System.DateTime.Now.TimeOfDay.Milliseconds,nodes,total3Dworkers)
    startAlgo algo total3Dworkers nodes

// -------Topologies end---------//

// Function to start topology setup based on inputs provided
let setupTopology totalworkers topology algorithm =
    if(topology = "full")
    then createFullNetwork totalworkers algorithm
    else if(topology = "3D")
    then create3DNetwork totalworkers algorithm
    else if(topology = "line")
    then createLineNetwork totalworkers algorithm
    else if(topology = "imp3D")
    then createImp3DNetwork totalworkers algorithm
    else
        printfn "Wrong Topology name entered!"

// Starting point
let init =
    let args = fsi.CommandLineArgs |> Array.tail
    let mutable totalworkers = args.[0] |> int
    let topo = args.[1] |> string
    let algorithm = args.[2] |> string
    setupTopology totalworkers topo algorithm
    System.Console.ReadLine() |> ignore
init