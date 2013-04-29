using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TSOdecrypt
{
    class LoopSubdivision
    {
        public const int ALPHA_MAX = 20;
        public const double ALPHA_LIMIT = 0.469;
        public double[] ALPHA = new double[] {1.13333, -0.358974, -0.333333, 0.129032, 0.945783, 2.0,
						                      3.19889, 4.47885, 5.79946, 7.13634, 8.47535, 9.80865,
						                      11.1322, 12.4441, 13.7439, 15.0317, 16.3082, 17.574,
						                      18.83, 20.0769};

        public const int BETA_MAX = 20;
        public const double BETA_LIMIT = 0.469;
        public double[] BETA = new double[]  {0.46875, 1.21875, 1.125, 0.96875, 0.840932, 0.75, 0.686349,
						                      0.641085, 0.60813, 0.583555, 0.564816, 0.55024, 0.5387,
						                      0.529419, 0.52185, 0.515601, 0.510385, 0.505987, 0.502247, 0.49904 };

        public const double NEWVERTEX = 123.0;

        public class Triangle
        {
            public UInt32[] verts;
        }

        public class Vertex
        {
            public double x, y, z;
            public bool newpoint;
            public Vertex averaged;
            public Vertex next;
        }

        public class Edge
        {
            public Vertex head;
            public Vertex tail;

            public Edge next;
            public Edge prev;
            public Edge twin;

            public Face left;
            public Face right;
        }

        public class Face
        {
            public Edge edge;
            public Vertex normal;
            public Face next;
        }

        public class WingedEdge
        {
            public Face faces;
            public Vertex vertices;
        }

        public class Model
        {
            public UInt32 numVertices;
            public UInt32 NumFaces;
            public Vertex position;
        }

        public Double alpha(UInt32 n)
        {
            double b;

            if (n <= 20) return ALPHA[n - 1];

            b = beta(n);

            return n * (1 - b) / b;
        }

        public Double beta(UInt32 n)
        {
            return (5.0 / 4.0 - (3 + 2 * Math.Cos(2 * Math.PI / n)) * (3 + 2 * Math.Cos(2 * Math.PI / n)) / 32);
        }

        public UInt32 vertexCount(ref WingedEdge we)
        {
            Vertex vertex;
            UInt32 count;

            count = 0;
            vertex = we.vertices;
            while (vertex != null)
            {
                count++;
                vertex = vertex.next;
            }
            return count;
        }

        public UInt32 faceCount(ref WingedEdge we)
        {
            Face face;
            UInt32 count;

            count = 0;
            face = we.faces;
            while (face != null)
            {
                count++;
                face = face.next;
            }
            return count;
        }

        public Vertex midpoint(ref Vertex v1, ref Vertex v2)
        {
	        Vertex m;

	        m = new Vertex();
	        m.x = (v1.x + v2.x) / 2.0;
	        m.y = (v1.y + v2.y) / 2.0;
	        m.z = (v1.z + v2.z) / 2.0;
	        return m;
        }

        public void verify(ref WingedEdge we)
        {
            Vertex vertex;
            Edge edge;
            Face face;
            UInt32 i;
            UInt32 n;

            vertex = we.vertices;
            n = 0;
            while (vertex != null)
            {
                n++;
                vertex = vertex.next;
            }
            System.Console.Out.WriteLine("{0} vertices\n", n.ToString());

            face = we.faces;
            n = 0;

            while (face != null)
            {
                edge = face.edge;
                i = 0;
                do
                {
                    if (edge.twin.twin != edge)
                        System.Console.Out.WriteLine("F:{0} E:{1} -- TWINS DON\'T MATCH!\n", n.ToString(), i.ToString());
                    if (edge.next.next.next != edge)
                        System.Console.Out.WriteLine("F:{0} E:{1} -- NEXT EDGE IS NOT PART OF TRIANGLE!!\n", n.ToString(), i.ToString());
                    if (edge.prev.prev.prev != edge)
                        System.Console.Out.WriteLine("F:{0} E:[1] -- PREV EDGE IS NOT PART OF TRIANGLE!!\n", n.ToString(), i.ToString());
                    if (edge.tail != edge.twin.head)
                        System.Console.Out.WriteLine("F:{0} E:{1} -- TWIN HAS DIFFERENT VERTS!!\n", n.ToString(), i.ToString());
                    edge = edge.next;
                    i++;
                } while (edge != face.edge);

                if (i != 3) System.Console.Out.WriteLine("TRIANGLE CONTAINS {0} EDGES!\n", i.ToString());

                n++;
                face = face.next;
            }
            System.Console.Out.WriteLine("{0} faces\n", n.ToString());
        }

        public void refine(ref WingedEdge we)
        {
	        Vertex vertex;
	        Vertex avg;
	        Face face;
	        Edge edge;
	        Edge inneredge;
	        Edge firstEdge;
	        UInt32 n;
	
	        vertex = we.vertices;

	        while (vertex != null)
            {
		        vertex.averaged = null;
		        vertex = vertex.next;
            }

	        face = we.faces;
	
	        while (face != null)
            {
		        edge = face.edge;
		
		        do 
                {
			        if (edge.head.averaged == null)
                    {
				        inneredge = edge;
				        n = 0;
				        edge.head.averaged = new Vertex();
				        avg = edge.head.averaged;
				        avg.x = avg.y = avg.z = 0.0;
				        do 
                        {
					        avg.x += inneredge.tail.x;
					        avg.y += inneredge.tail.y;
					        avg.z += inneredge.tail.z;
					        inneredge = inneredge.next.twin;
					        n++;
				        } 
                        while (inneredge != edge);

				        avg.x += edge.head.x * alpha(n);
				        avg.y += edge.head.y * alpha(n);
				        avg.z += edge.head.z * alpha(n);
				        avg.x /= alpha(n) + n;
				        avg.y /= alpha(n) + n;
				        avg.z /= alpha(n) + n;
			        }
			        edge = edge.next;
		        } 
                while (edge != face.edge);
		        face = face.next;
	        }

	        vertex = we.vertices;

	        while (vertex != null)
            {
		        vertex.x = vertex.averaged.x;
		        vertex.y = vertex.averaged.y;
		        vertex.z = vertex.averaged.z;

		        vertex = vertex.next;
	        }
        }

        public void secondDivision(ref Face faces)
        {
            Edge edge;
            Vertex vertex;
            Vertex m;
            Edge newedge;
            Edge twinedge;
            Face face;
            Face newface;
            Face faceptr;

            face = faces;

            /* first let's add some new edges */
            while (face != null)
            {

                edge = face.edge;

                do
                {
                    newedge = new Edge();

                    newedge.head = edge.prev.tail;
                    newedge.tail = edge.head;
                    newedge.next = edge.prev;
                    newedge.prev = edge;
                    newedge.left = null;

                    /* this is how we'll keep track of the new edges */
                    edge.prev.prev = newedge;

                    twinedge = new Edge();
                    twinedge.head = newedge.tail;
                    twinedge.tail = newedge.head;
                    twinedge.twin = newedge;
                    newedge.twin = twinedge;
                    twinedge.left = null;

                    edge = edge.next.next;
                }
                while (edge != face.edge);

                face = face.next;
            }

            face = faces;
            while (face != null)
            {
                if (face.edge.right != face.edge.left)
                { /* make sure to skip the new faces */
                    /* add the 3 corner faces (actually, we reuse one) */
                    newedge = edge = face.edge;
                    do
                    {
                        if (edge != newedge)
                        { /* is this really a new face? */
                            newface = new Face();
                            /* add the new face to the list */
                            faceptr = face.next;
                            face.next = newface;
                            newface.next = faceptr;
                        }
                        else
                        {
                            newface = face;
                        }
                        /* tell all the edges about this new face */
                        edge.left = newface;
                        edge.prev.left = newface;
                        edge.prev.prev.left = newface;

                        /* assign an edge to the new face */
                        newface.edge = edge;
                        edge.right = edge.left; /* mark this face as new! */
                        edge = edge.next.next;
                    }
                    while (edge != newedge);

                    edge = face.edge;

                    edge.right = edge.left;

                    /* add the inner face */
                    newface = new Face();

                    /* add the new face to the list */
                    faceptr = face.next;
                    face.next = newface;
                    newface.next = faceptr;

                    /* assign an edge to the new face */
                    newface.edge = edge.prev.prev.twin;

                    /* complete the inner edges */
                    do
                    {
                        newedge = edge.prev.prev.twin;
                        newedge.next = edge.next.prev.twin;
                        newedge.next.prev = newedge;
                        /* tell all the edges about this new face */
                        newedge.left = newface;
                        newedge.right = newedge.left;
                        edge = edge.next.next;
                    }
                    while (edge != face.edge);

                    /* complete the outer edges */
                    edge = face.edge;
                    do
                    {
                        edge.next = edge.prev.prev;
                        edge = edge.next.twin.next.twin.prev; /* phew! */
                    }
                    while (edge != face.edge);
                    edge = face.edge;
                    do
                    {
                        edge.prev.prev = edge.next;
                        edge = edge.next.twin.next.twin.prev; /* phew! */
                    }
                    while (edge != face.edge);
                }
                face = face.next;
            }
            face = faces;
            while (face != null)
            { /* now to add those right faces */
                edge = face.edge;
                do
                {
                    edge.right = edge.twin.left;
                    edge = edge.next;
                }
                while (edge != face.edge);

                face = face.next;
            }
        }

        public void firstDivision(ref Face face)
        {
            Edge edge;
            Vertex vertex;
            Vertex m;
            Edge newedge;
            Edge twinedge;

            edge = face.edge;

            do
            {

                if (edge.next.twin.next.twin != edge)
                {
                    /* There is work here to be done! */
                    /* get the midpoint */

                    m = midpoint(ref edge.head, ref edge.tail);
                    m.newpoint = true;/* flag this one as new! */

                    /* insert the new vertex into the list */
                    vertex = edge.tail.next;
                    edge.tail.next = m;
                    m.next = vertex;

                    /* create new edge */
                    newedge = new Edge();
                    newedge.head = edge.head;
                    newedge.tail = m;
                    newedge.prev = edge;
                    newedge.next = edge.next;
                    edge.next.prev = newedge;
                    edge.next = newedge;
                    newedge.twin = edge.twin;
                    edge.head = m;
                    edge.twin.twin = newedge;

                    /* create a new edge for the twin */
                    twinedge = new Edge();
                    twinedge.head = edge.twin.head;
                    twinedge.tail = m;
                    twinedge.prev = edge.twin;
                    twinedge.next = edge.twin.next;
                    edge.twin.next.prev = twinedge;
                    edge.twin.next = twinedge;
                    twinedge.twin = edge;
                    edge.twin.head = m;
                    edge.twin = twinedge;

                    firstDivision(ref edge.next.twin.left);
                }
                edge = edge.next.next;
            }
            while (edge != face.edge);
        }

        public void subdivide(ref WingedEdge we)
        {
            Vertex vertex;

            vertex = we.vertices;
            while (vertex != null)
            {
                vertex.newpoint = false;
                vertex = vertex.next;
            }
            firstDivision(ref we.faces);
            secondDivision(ref we.faces);
        }
        
        public bool sequal(ref Vertex u, ref Vertex v, Double epsilon)
        {
            if (sabs(u.x - v.x) < epsilon &&
                sabs(u.y - v.y) < epsilon &&
                sabs(u.z - v.z) < epsilon)
            {
                return true;
            }
            return false;
        }

        public Double sabs(Double f)
        {
            return (f < 0 ? -f : f);
        }

        public void snormalize(ref Vertex v)
        {
            double l;


            l = (double)Math.Sqrt(sdot(ref v, ref v));
            v.x /= l;
            v.y /= l;
            v.z /= l;
        }

        public double sdot(ref Vertex u, ref Vertex v)
        {

            return u.x * v.x + u.y * v.y + u.z * v.z;
        }

        public Vertex scross(ref Vertex u, ref Vertex v)
        {
	        Vertex result;

	        result = new Vertex();

	        result.x = u.y * v.z - u.z * v.y;
	        result.y = u.z * v.x - u.x * v.z;
	        result.z = u.x * v.y - u.y * v.x;

	        return result;
        }

        public double smax(double a, double b)
        {
            return (b > a ? b : a);
        }

        public void facetNormals(ref WingedEdge we)
        {
            Face face;
            Edge edge;
            Vertex edge1;
            Vertex edge2;

            /* and the rest we'll just pretend are ok */

            face = we.faces;

            while (face != null)
            {
                edge1 = new Vertex();
                edge2 = new Vertex();

                edge1.x = face.edge.tail.x - face.edge.head.x;
                edge1.y = face.edge.tail.y - face.edge.head.y;
                edge1.z = face.edge.tail.z - face.edge.head.z;

                edge2.x = face.edge.prev.tail.x - face.edge.prev.head.x;
                edge2.y = face.edge.prev.tail.y - face.edge.prev.head.y;
                edge2.z = face.edge.prev.tail.z - face.edge.prev.head.z;

                face.normal = scross(ref edge1, ref edge2);
                snormalize(ref face.normal);


                face = face.next;
            }
        }

        public double unitize(ref WingedEdge we)
        {
            double maxx, minx, maxy, miny, maxz, minz;
            double cx, cy, cz, w, h, d;
            double scale;
            Vertex vertex;

            /* and we'll just hope that the rest is ok */

            vertex = we.vertices;

            /* get the max/mins */
            maxx = minx = vertex.x;
            maxy = miny = vertex.y;
            maxz = minz = vertex.z;
            while (vertex != null)
            {
                if (maxx < vertex.x) maxx = vertex.x;
                if (minx > vertex.x) minx = vertex.x;

                if (maxy < vertex.y) maxy = vertex.y;
                if (miny > vertex.y) miny = vertex.y;

                if (maxz < vertex.z) maxz = vertex.z;
                if (minz > vertex.z) minz = vertex.z;

                vertex = vertex.next;
            }

            /* calculate model width, height, and depth */
            w = sabs(maxx) + sabs(minx);
            h = sabs(maxy) + sabs(miny);
            d = sabs(maxz) + sabs(minz);

            /* calculate center of the model */
            cx = (maxx + minx) / 2.0;
            cy = (maxy + miny) / 2.0;
            cz = (maxz + minz) / 2.0;

            /* calculate unitizing scale factor */
            scale = 2.0 / Math.Sqrt(w * w + h * h + d * d);

            /* translate around center then scale */
            vertex = we.vertices;
            while (vertex != null)
            {
                vertex.x -= cx;
                vertex.y -= cy;
                vertex.z -= cz;
                vertex.x *= scale;
                vertex.y *= scale;
                vertex.z *= scale;

                vertex = vertex.next;
            }

            return scale;
        }


    }
}
