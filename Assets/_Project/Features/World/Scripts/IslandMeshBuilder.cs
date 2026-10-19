using System.Collections.Generic;
using Leeway.World.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Leeway.World
{
    /// <summary>
    /// Turns an <see cref="IslandHeightField"/> into meshes: flat-shaded terrain chunks (every
    /// triangle has its own vertices, so faces read as low-poly facets) coloured per triangle,
    /// lighter shared-vertex collider chunks and the water surface (sea, lakes, rivers).
    /// </summary>
    public static class IslandMeshBuilder
    {
        /// <summary>Terrain for the cells [x0, x1) x [z0, z1). Null when the chunk is all deep sea floor.</summary>
        public static Mesh BuildTerrainChunk(IslandHeightField field, IslandBiome biome, int x0, int z0, int x1, int z1)
        {
            IslandSettings s = field.Settings;
            var patches = new IslandNoise(field.Seed + 101);
            var palette = new IslandGroundPalette(biome);
            float deepFloor = s.SeaFloorDepth + 0.25f;

            int cells = (x1 - x0) * (z1 - z0);
            var vertices = new List<Vector3>(cells * 6);
            var normals = new List<Vector3>(cells * 6);
            var colors = new List<Color>(cells * 6);

            for (int z = z0; z < z1; z++)
            for (int x = x0; x < x1; x++)
            {
                Vector3 v00 = field.VertexPosition(x, z);
                Vector3 v10 = field.VertexPosition(x + 1, z);
                Vector3 v01 = field.VertexPosition(x, z + 1);
                Vector3 v11 = field.VertexPosition(x + 1, z + 1);

                // Diagonal v00-v11 everywhere - IslandHeightField.SampleHeight assumes it.
                AddTriangle(v00, v11, v10);
                AddTriangle(v00, v01, v11);
            }

            if (vertices.Count == 0) return null;

            var mesh = new Mesh { name = $"Island_{field.Seed}_{x0}_{z0}", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(Sequence(vertices.Count), 0);
            mesh.RecalculateBounds();
            return mesh;

            void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
            {
                // The sea plane hides the deep floor; skipping it saves most of the outer ring.
                if (a.y < deepFloor && b.y < deepFloor && c.y < deepFloor) return;

                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                Vector3 centre = (a + b + c) / 3f;
                Color color = TriangleColor(centre, normal);

                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                colors.Add(color); colors.Add(color); colors.Add(color);
            }

            Color TriangleColor(Vector3 centre, Vector3 normal)
            {
                return palette.At(field, patches, centre, normal);
            }
        }

        /// <summary>Shared-vertex collider for the same cell range as <see cref="BuildTerrainChunk"/>.</summary>
        public static Mesh BuildColliderChunk(IslandHeightField field, int x0, int z0, int x1, int z1)
        {
            int w = x1 - x0 + 1;
            int h = z1 - z0 + 1;
            var vertices = new Vector3[w * h];
            for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
                vertices[z * w + x] = field.VertexPosition(x0 + x, z0 + z);

            var triangles = new int[(w - 1) * (h - 1) * 6];
            int t = 0;
            for (int z = 0; z < h - 1; z++)
            for (int x = 0; x < w - 1; x++)
            {
                int i00 = z * w + x;
                int i10 = i00 + 1;
                int i01 = i00 + w;
                int i11 = i01 + 1;
                triangles[t++] = i00; triangles[t++] = i11; triangles[t++] = i10;
                triangles[t++] = i00; triangles[t++] = i01; triangles[t++] = i11;
            }

            var mesh = new Mesh { name = $"IslandCollider_{field.Seed}_{x0}_{z0}", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A continuous water datum shared by neighbouring terrain triangles, clipped against the
        /// exact same terrain diagonal. Shore edges are real intersections, not whole wet quads.
        /// Colour: r=depth, g=fresh water. Flow UVs are attributes of this same surface.
        /// </summary>
        public static Mesh BuildWaterSurface(IslandHeightField field, float depthRange = 3f)
        {
            int n = field.VertexCount;
            float sea = field.Settings.SeaLevel;
            var levels = new float[n*n];
            var fresh = new bool[n*n];
            var ground = new float[n*n];
            var wet = new bool[n*n];
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                int i=z*n+x;
                ground[i]=field.HeightAt(x,z);
                levels[i]=field.WaterSurfaceAt(x,z);
                fresh[i]=field.IsFreshSurfaceAt(x,z);
                wet[i]=ground[i]<levels[i]-0.001f;
            }

            var flowUV=new Vector2[n*n];var flowAmount=new float[n*n];var closest=new float[n*n];
            for(int i=0;i<closest.Length;i++)closest[i]=float.PositiveInfinity;
            foreach(var river in field.Rivers)
            {
                float along=0;
                for(int j=1;j<river.Points.Count;j++)
                {
                    Vector3 a=river.Points[j-1],b=river.Points[j];
                    Vector2 start=new Vector2(a.x,a.z),delta=new Vector2(b.x-a.x,b.z-a.z);
                    float length2=delta.sqrMagnitude,segmentLength=Vector3.Distance(a,b);
                    if(length2<0.00001f)continue;
                    float reach=river.Width*0.8f;
                    Vector2 side=new Vector2(delta.y,-delta.x)/Mathf.Sqrt(length2);
                    int x0=Mathf.Max(0,Mathf.FloorToInt((Mathf.Min(a.x,b.x)-reach+field.Settings.HalfSize)/field.Settings.CellSize));
                    int x1=Mathf.Min(n-1,Mathf.CeilToInt((Mathf.Max(a.x,b.x)+reach+field.Settings.HalfSize)/field.Settings.CellSize));
                    int z0=Mathf.Max(0,Mathf.FloorToInt((Mathf.Min(a.z,b.z)-reach+field.Settings.HalfSize)/field.Settings.CellSize));
                    int z1=Mathf.Min(n-1,Mathf.CeilToInt((Mathf.Max(a.z,b.z)+reach+field.Settings.HalfSize)/field.Settings.CellSize));
                    for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
                    {
                        int i=z*n+x;if(!fresh[i])continue;
                        Vector2 p=new Vector2(-field.Settings.HalfSize+x*field.Settings.CellSize,-field.Settings.HalfSize+z*field.Settings.CellSize);
                        float t=Mathf.Clamp01(Vector2.Dot(p-start,delta)/length2);
                        Vector2 offset=p-start-delta*t;
                        float d=offset.sqrMagnitude,error=Mathf.Abs(levels[i]-Mathf.Lerp(a.y,b.y,t));
                        float score=d+error*error;
                        if(d>reach*reach||score>=closest[i])continue;
                        closest[i]=score;flowUV[i]=new Vector2(0.5f+Vector2.Dot(offset,side)/river.Width,along+t*segmentLength);
                        flowAmount[i]=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(river.Width*0.5f,reach,Mathf.Sqrt(d)));
                    }
                    along+=segmentLength;
                }
            }
            var vertices=new List<Vector3>();
            var normals=new List<Vector3>();
            var colors=new List<Color>();
            var uvs=new List<Vector2>();var flows=new List<Vector2>();
            var triangles=new List<int>();
            var polygon=new WaterVertex[4];
            var input=new WaterVertex[3];
            for(int z=0;z<n-1;z++)
            for(int x=0;x<n-1;x++)
            {
                int a=z*n+x,b=a+1,c=a+n,d=c+1;
                AddClipped(a,d,b);
                AddClipped(a,c,d);
            }
            if(vertices.Count==0)return null;
            var mesh=new Mesh{name=$"IslandWater_{field.Seed}",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetUVs(0,uvs);mesh.SetUVs(1,flows);mesh.SetTriangles(triangles,0);
            mesh.RecalculateBounds();
            var bounds=mesh.bounds;bounds.Expand(2f);mesh.bounds=bounds;
            return mesh;

            void AddClipped(int a,int b,int c)
            {
                if(!wet[a]&&!wet[b]&&!wet[c])return;
                input[0]=At(a);input[1]=At(b);input[2]=At(c);
                int count=0;
                WaterVertex previous=input[2];
                for(int k=0;k<3;k++)
                {
                    WaterVertex current=input[k];
                    bool inside=current.Depth>=0,wasInside=previous.Depth>=0;
                    if(inside!=wasInside)
                    {
                        float t=previous.Depth/(previous.Depth-current.Depth);
                        polygon[count++]=new WaterVertex
                        {
                            Position=Vector3.Lerp(previous.Position,current.Position,t),
                            Depth=0,
                            Fresh=Mathf.Lerp(previous.Fresh,current.Fresh,t),FlowUV=Vector2.Lerp(previous.FlowUV,current.FlowUV,t),FlowAmount=Mathf.Lerp(previous.FlowAmount,current.FlowAmount,t),Normal=Vector3.Lerp(previous.Normal,current.Normal,t).normalized
                        };
                    }
                    if(inside)polygon[count++]=current;
                    previous=current;
                }
                if(count<3)return;
                for(int k=1;k<count-1;k++)
                {
                    Vector3 cross=Vector3.Cross(polygon[k].Position-polygon[0].Position,
                        polygon[k+1].Position-polygon[0].Position);
                    if(cross.sqrMagnitude<1e-10f)continue;
                    int start=vertices.Count;
                    Add(polygon[0]);Add(polygon[k]);Add(polygon[k+1]);
                    triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);
                }
            }
            WaterVertex At(int i)
            {
                Vector3 p=field.VertexPosition(i%n,i/n);p.y=levels[i];
                int x=i%n,z=i/n;float cell=field.Settings.CellSize;
                int left=Mathf.Max(0,x-1),right=Mathf.Min(n-1,x+1),back=Mathf.Max(0,z-1),front=Mathf.Min(n-1,z+1);
                float dx=(field.WaterSurfaceAt(right,z)-field.WaterSurfaceAt(left,z))/Mathf.Max(cell,(right-left)*cell);
                float dz=(field.WaterSurfaceAt(x,front)-field.WaterSurfaceAt(x,back))/Mathf.Max(cell,(front-back)*cell);
                Vector3 normal=new Vector3(-dx,1,-dz).normalized;
                return new WaterVertex{Position=p,Depth=levels[i]-ground[i],Fresh=fresh[i]?1:0,FlowUV=flowUV[i],FlowAmount=flowAmount[i],Normal=normal};
            }
            void Add(WaterVertex v)
            {
                // Millimetre bias prevents shoreline z-fighting without a visible floating bank.
                vertices.Add(v.Position+Vector3.up*0.008f);
                normals.Add(v.Normal);
                colors.Add(new Color(Mathf.Clamp01(v.Depth/Mathf.Max(depthRange,0.01f)),v.Fresh,0,0));
                uvs.Add(v.FlowUV);flows.Add(new Vector2(v.FlowAmount,0));
            }
        }

        private struct WaterVertex
        {
            public Vector3 Position,Normal;
            public float Depth,Fresh,FlowAmount;
            public Vector2 FlowUV;
        }

        /// <summary>Concentric tessellated rings; the inner boundary matches the terrain grid exactly.</summary>
        public static Mesh BuildSeaRing(float outerSize,float innerSize,float height,float cellSize=2.5f)
        {
            float inner=Mathf.Max(1,innerSize)*0.5f,outer=Mathf.Max(outerSize,innerSize+2)*0.5f;
            int sideCount=Mathf.Max(4,Mathf.CeilToInt(innerSize/Mathf.Max(cellSize,0.5f)));
            int perimeter=sideCount*4;
            var vertices=new List<Vector3>();var normals=new List<Vector3>();
            var colors=new List<Color>();var triangles=new List<int>();
            float radius=inner,spacing=Mathf.Max(0.5f,cellSize);
            int ring=0;
            for(int ringLimit=0;ringLimit<128;ringLimit++)
            {
                for(int side=0;side<4;side++)
                for(int i=0;i<sideCount;i++)
                {
                    float u=i/(float)sideCount,t=Mathf.Lerp(-radius,radius,u);
                    Vector3 p=side==0?new Vector3(t,height,-radius):
                        side==1?new Vector3(radius,height,t):
                        side==2?new Vector3(-t,height,radius):new Vector3(-radius,height,-t);
                    vertices.Add(p);normals.Add(Vector3.up);colors.Add(new Color(1,0,0,0));
                }
                if(ring>0)
                for(int i=0;i<perimeter;i++)
                {
                    int next=(i+1)%perimeter,a=(ring-1)*perimeter+i,b=(ring-1)*perimeter+next;
                    int c=ring*perimeter+i,d=ring*perimeter+next;
                    triangles.Add(a);triangles.Add(b);triangles.Add(c);
                    triangles.Add(b);triangles.Add(d);triangles.Add(c);
                }
                if(radius>=outer)break;
                radius=Mathf.Min(outer,radius+spacing);spacing*=1.35f;ring++;
            }
            var mesh=new Mesh{name="IslandOpenSea",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);
            mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            var bounds=mesh.bounds;bounds.Expand(2f);mesh.bounds=bounds;
            return mesh;
        }


        public const int MaxWaterImpacts=24;
        public struct WaterImpact
        {
            public Vector3 Position,Direction;
            public float Radius;
            public bool Ocean;
        }
        /// <summary>Receiver-side splash zones, prioritising river/ocean mouths.</summary>
        public static List<WaterImpact> BuildWaterImpacts(IslandHeightField field)
        {
            var result=new List<WaterImpact>();
            float sea=field.Settings.SeaLevel;
            foreach(var river in field.Rivers)
            for(int i=1;i<river.Points.Count;i++)
            {
                Vector3 a=river.Points[i-1],b=river.Points[i];
                float before=field.SampleWaterSurface(a.x,a.z),after=field.SampleWaterSurface(b.x,b.z);
                if(after>sea+0.12f||before<=sea+0.12f)continue;
                Vector3 direction=b-a;direction.y=0;direction.Normalize();
                Vector3 point=b;point.y=sea+0.008f;
                Add(new WaterImpact{Position=point,Direction=direction,Radius=river.Width*1.05f,Ocean=true});break;
            }
            foreach(var river in field.Rivers)
            {
                float runDrop=0;
                for(int i=1;i<river.Points.Count;i++)
                {
                    Vector3 a=river.Points[i-1],b=river.Points[i],delta=b-a;
                    float drop=-delta.y;delta.y=0;
                    bool steep=drop>0.7f&&drop/Mathf.Max(delta.magnitude,0.01f)>0.45f;
                    if(steep)runDrop+=drop;
                    bool nextSteep=false;
                    if(i+1<river.Points.Count)
                    {Vector3 next=river.Points[i+1]-b;float nextDrop=-next.y;next.y=0;nextSteep=nextDrop>0.7f&&nextDrop/Mathf.Max(next.magnitude,0.01f)>0.45f;}
                    if(runDrop>1.1f&&!nextSteep)
                    {
                        Vector3 direction=delta.normalized,point=b+direction*field.Settings.CellSize*0.35f;
                        float height=field.SampleWaterSurface(point.x,point.z);
                        if(height>sea+0.2f)
                        {point.y=height+0.008f;Add(new WaterImpact{Position=point,Direction=direction,Radius=river.Width*0.9f});}
                        runDrop=0;
                    }
                    if(!steep)runDrop=0;
                }
            }
            return result;
            void Add(WaterImpact impact)
            {
                foreach(var old in result)
                    if(Mathf.Abs(old.Position.y-impact.Position.y)<0.8f&&
                       Vector2.Distance(new Vector2(old.Position.x,old.Position.z),new Vector2(impact.Position.x,impact.Position.z))
                        <Mathf.Min(old.Radius,impact.Radius)*0.85f)return;
                if(result.Count<MaxWaterImpacts)result.Add(impact);
            }
        }

        private static int[] Sequence(int count)
        {
            var indices = new int[count];
            for (int i = 0; i < count; i++) indices[i] = i;
            return indices;
        }

        private static float Hash(Vector3 p)
        {
            float h = Mathf.Sin(p.x * 12.9898f + p.z * 78.233f + p.y * 37.719f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }
    }
}
