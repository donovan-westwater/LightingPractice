using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriangleAdjSearch : MonoBehaviour
{
    public MeshFilter model;
    public Shader crossHatch;
    private Mesh readOnlyMesh;
    private Material modelMat;
    private ComputeBuffer triangleBuffer;
    struct Triangle
    {
        public int id;
        public Vector3 adjVertIds; //Get the remaining vertex in the adj trianlges not included in the current one
    }
    List<Triangle> triangles = new List<Triangle>();
    Dictionary<Vector2Int, List<int>> adjcentTriDict; //Maps an edge (stored as vec2int) with the triangles it shares to the index of the triangle List
    
    void addTri(Vector2Int eId,int tId)
    {
        if (!adjcentTriDict.ContainsKey(eId))
        {
            List<int> tmp = new List<int>();
            tmp.Add(tId);
            adjcentTriDict.Add(eId,tmp);
        }
        else
        {
            adjcentTriDict[eId].Add(tId);
        }
    }
    Vector2Int CreateEdge(int v1Id,int v2Id)
    {
        Vector2Int o = new Vector2Int(0, 0);
        Vector3 v1 = readOnlyMesh.vertices[v1Id];
        Vector3 v2 = readOnlyMesh.vertices[v1Id];
        if (v1.x < v2.x) {
            o.x = v1Id;
            o.y = v2Id;
        }else if(v1.x == v2.x && v1.y < v2.y)
        {
            o.x = v1Id;
            o.y = v2Id;
        }else if (v1.x == v2.x && v1.y == v2.y && v1.z < v2.z)
        {
            o.x = v1Id;
            o.y = v2Id;
        }
        else
        {
            o.x = v2Id;
            o.y = v1Id;
        }
        //The edge should be unique! The reversed edge should equal this edge. The reversed version shouldnt show up at all.
        Debug.Assert(!adjcentTriDict.ContainsKey(new Vector2Int(o.y, o.x)));
        return o;
    }
    int IsolateRemainingVert(Vector2Int e,int tId)
    {
        //Find the Adjcent Triangle
        int adjT = -1;
        foreach(int j in adjcentTriDict[e])
        {
            if(j != tId)
            {
                adjT = j;
                break;
            }
        }
        //Get the vertex ids for our current triangle
        int[] v = { readOnlyMesh.triangles[adjT * 3],
            readOnlyMesh.triangles[adjT * 3 + 1],
            readOnlyMesh.triangles[adjT * 3 + 2 ] };
        foreach(int i in v)
        {
            //If vertex is not in the shared edge, then we know where the remaing vertex is
            if (i != e.x && i != e.y)
            {
                adjT = i;
                break;
            }
        }
        return adjT;
    }
    // Start is called before the first frame update
    void Start()
    {
        model = this.GetComponent<MeshFilter>();
        if (model == null || crossHatch == null) return;
        modelMat = new Material(crossHatch);
        readOnlyMesh = model.mesh;
        adjcentTriDict = new Dictionary<Vector2Int, List<int>>();
        //Add the triangles to the triangles
        int tId = 0;
        for (int i = 2; i < readOnlyMesh.triangles.Length; i+=3)
        {
            Triangle t = new Triangle();
            t.adjVertIds = new Vector3(-1, -1, -1);
            t.id = tId;
            addTri(CreateEdge(readOnlyMesh.triangles[i - 2], readOnlyMesh.triangles[i - 1])
                , tId);
            addTri(CreateEdge(readOnlyMesh.triangles[i - 2], readOnlyMesh.triangles[i - 0])
                , tId);
            addTri(CreateEdge(readOnlyMesh.triangles[i - 1], readOnlyMesh.triangles[i - 0])
                , tId);
            triangles.Add(t);
            tId++;
            Debug.Log(tId);
        }
        //Populate adj Vector in triangles by going through the edges
        //of each triangle and finding their associated traingles which aren't the current one
        tId = 0;
        for (int i = 2; i < readOnlyMesh.triangles.Length; i+=3)
        {
            
            Vector2Int e1 = CreateEdge(readOnlyMesh.triangles[i - 2], readOnlyMesh.triangles[i - 1]);
            Vector2Int e2 = CreateEdge(readOnlyMesh.triangles[i - 2], readOnlyMesh.triangles[i - 0]);
            Vector2Int e3 = CreateEdge(readOnlyMesh.triangles[i - 1], readOnlyMesh.triangles[i - 0]);
            //Isolate the remaining vert
            int v1 = IsolateRemainingVert(e1, tId);
            int v2 = IsolateRemainingVert(e2, tId);
            int v3 = IsolateRemainingVert(e3, tId);
            Debug.Assert(v1 > 0 && v2 > 0 && v3 > 0);
            //Add the adjcent triangle ids to triangles
            Triangle tmp = triangles[tId];
            tmp.adjVertIds = new Vector3(v1, v2, v3);
            triangles[tId] = tmp;
            tId++;
            Debug.Log("Round 2: "+tId);
            
        }
        int stride = sizeof(float)*3+sizeof(int);
        triangleBuffer = new ComputeBuffer(triangles.Count,stride);
        triangleBuffer.SetData(triangles.ToArray());
        modelMat.SetInt("bufferCount", triangles.Count);
        modelMat.SetBuffer("triangleBuffer", triangleBuffer);
        GetComponent<Renderer>().material = modelMat;
    }
}
