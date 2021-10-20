# Gossip_simulator
**Brief Description**
The aim of the project is to determine the convergence time of 2 algorithms, namely Gossip and Push-Sum, using a simulator that works on actors arranged in different topology (Line, Full, 3D and Imperfect 3D). Both these algorithms are widely used in the distributed system to spread news robustly, system monitoring, broadcast and multicast, etc.
There are 2 sections, one for each algorithm with all 4 topologies in the respective graph. Each graph contains Time in the Y-axis and Number of nodes in the X-axis.

**Gossip**
In gossip algorithm the convergence time increases with the number of nodes and is slowest (max time) for line and fastest for the full topology.

**Push-Sum**
For push-sum algorithm, the fastest topology or the one that took the least time to converge is Full and slowest or with the max time is Line.

**Observations/Interesting findings:**
• Convergence rate from fastest to slowest, in both Gossip and Push-sum was same: o Full > Imp3D > 3D > Line
• We could observe that for both the algorithms, the convergence speed is directly dependent on the number of nodes. The more the no. of Nodes the more the time program takes to converge.
• Full topology was the fastest as every node is connected to every other node thus making the spread of rumor fast.
• We also observed that for Imperfect 3D topology, time taken is lesser than 3D as it has an extra neighbor compared to 3D.
• In all cases, line was the slowest as each node is only connected to 2 other nodes.
