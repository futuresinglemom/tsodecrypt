using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;


namespace TSOdecrypt
{
    class Program
    {
        static public bool show_warnings = false;
        static void Main(string[] args)
        {
            while (true)
            {
                string source_file = "";
                long last_key_event = System.DateTime.Now.Ticks;
                System.Console.Out.WriteLine("Simply Drag and Drop a TSO file on this decrypter.");
                System.Console.Out.WriteLine("The converted data will then be written to a");
                System.Console.Out.WriteLine("subfolder that is named like the TSO file.");
                System.Console.Out.WriteLine("This also works the other way around. Drag and Drop");
                System.Console.Out.WriteLine("the created folder on this decrypter and an");
                System.Console.Out.WriteLine("according TSO file will be created.");
                if (args.Length > 0)
                {
                    if (decrypt_TSO(args[0]) >= 0)
                    {
                        break;
                    }
                }
                while (true)
                {
                    ConsoleKeyInfo CKeyInfo = System.Console.ReadKey(true);
                    if (((source_file.Length == 1) && (last_key_event + 10000 < System.DateTime.Now.Ticks)) || (last_key_event + 10000000 < System.DateTime.Now.Ticks))
                    {
                        //last event seems not to be Drag and Drop but user input
                        source_file = "";
                    }
                    last_key_event = System.DateTime.Now.Ticks;
                    source_file += CKeyInfo.KeyChar;
                    if ((source_file.Length > 1) && source_file[source_file.Length - 1].Equals('"'))
                    {
                        System.Console.Out.WriteLine(source_file.Substring(1, source_file.Length - 2));
                        if (decrypt_TSO(source_file) >= 0)
                        {
                            break;
                        }
                    }
                    else if ((source_file.Length > 1) && !System.Console.KeyAvailable)
                    {
                        System.Console.Out.WriteLine(source_file);
                        if (decrypt_TSO(source_file) >= 0)
                        {
                            break;
                        }
                    }
                }
            }

            if (TSOdecrypt.Program.show_warnings)
            {
                System.Console.Out.WriteLine("Press any key to continue");
                Console.ReadKey(false);
            }
        }
        static int decrypt_TSO(string source_file)
        {
            if (source_file.Substring(source_file.Length - 3, 3).ToLower().Equals("tso"))
            {
                string dest_path = "";
                string[] sep = new string[1];
                sep[0] = "\\";
                string[] file_path = source_file.Split(sep, System.StringSplitOptions.RemoveEmptyEntries);
                string file_name = file_path[file_path.Length - 1];
                string folder_name = file_name.Substring(0, file_name.LastIndexOf("."));
                for (int i = 0; i < file_path.Length - 1; i++)
                {
                    dest_path += file_path[i] + "\\";
                }
                dest_path += folder_name;
                System.Console.Out.WriteLine(dest_path);
                Decrypter myDecrypter = new Decrypter();
                return myDecrypter.decrypt_TSO(source_file, dest_path);
            }
            else if (System.IO.Directory.Exists(source_file))
            {
                Encrypter myEncrypter = new Encrypter();
                return myEncrypter.encrypt_TSO(source_file);
            }
            else
            {
                System.Console.Out.WriteLine("Sorry, the TSO decrypter does not process such files.");
                return 0;
            }
        }
    }

    class Encrypter
    {
        System.IO.BinaryReader reader;
        public bool use_mesh_binary;
        public static System.Globalization.CultureInfo Culture = new System.Globalization.CultureInfo("en-US");

        public static UInt32 CACHESIZE_GEFORCE_3_4_5_6 = 16;//24; seems to be instable
        public enum PrimType
        {
            PT_LIST,
            PT_STRIP,
            PT_FAN
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe public struct PrimitiveGroup
        {
            [MarshalAs(UnmanagedType.U4)]
	        public PrimType type;
            [MarshalAs(UnmanagedType.U4)]
            public UInt32 numIndices;
            [MarshalAs(UnmanagedType.SysUInt)]
            public IntPtr indices;
        }

        public class scene
        {
            public bone_node[] skellettons; //each entry represents a root bone_node of a whole skelleton
            public texture[] textures;
            public script[] scripts;
            public List<mesh> meshes;
            public UInt32 bone_node_count;
            public List<material> mat_list;
            public string file_name_bin_mesh;


            public byte[] scene_skeleton_to_tso()
            {
                byte[] ret = null;
                string current_path = "";
                string recursive_skel = "";
                recursive_traverse_skeleton(current_path, ref recursive_skel, this.skellettons);
                byte[] recursive_skel_bytes = System.Text.Encoding.ASCII.GetBytes(recursive_skel);
                for (int i = 0; i < (recursive_skel_bytes.Length - 1); i++)
                {
                    if ((recursive_skel_bytes[i] == 0x25) && (recursive_skel_bytes[i + 1] == 0x7C))
                    {
                        recursive_skel_bytes[i] = 0x00;
                    }
                }
                recursive_skel_bytes[recursive_skel_bytes.Length - 1] = 0x00;
                //skeleton tree done... now the header for the transformatrices field
                byte[] header_matrices_field = new byte[5];
                header_matrices_field[0] = 0x00;
                byte[] entry_cnt = System.BitConverter.GetBytes((UInt32)(this.bone_node_count));
                entry_cnt.CopyTo(header_matrices_field, 1);
                //and now the matrices field itself...
                byte[] recursive_matrices = new byte[(int)(16 * 4 * this.bone_node_count)];
                int offset = 0;
                recursive_traverse_skeleton_matrices(ref offset, ref recursive_matrices, this.skellettons);
                ret = new byte[recursive_skel_bytes.Length + header_matrices_field.Length + recursive_matrices.Length - 1];
                recursive_skel_bytes.CopyTo(ret, 0);
                header_matrices_field.CopyTo(ret, recursive_skel_bytes.Length - 1);
                recursive_matrices.CopyTo(ret, recursive_skel_bytes.Length + header_matrices_field.Length - 1);
                return ret;
            }

            public void recursive_traverse_skeleton_matrices(ref int offset, ref byte[] recursive_matrices, bone_node[] root)
            {
                for (int i = 0; i < root.Length; i++)
                {
                    for (int j = 0; j < 16; j++)
                    {
                        byte[] single_val = System.BitConverter.GetBytes((Single)root[i].transformation_matrix[j]);
                        single_val.CopyTo(recursive_matrices, offset);
                        offset += 4;
                    }
                    if (root[i].child_nodes != null)
                    {
                        recursive_traverse_skeleton_matrices(ref offset, ref recursive_matrices, root[i].child_nodes);
                    }
                }
            }

            public void recursive_traverse_skeleton(string current_path, ref string recursive_skel, bone_node[] root)
            {
                string tcurrent_path = "";
                for (int i = 0; i < root.Length; i++)
                {
                    tcurrent_path += current_path;
                    tcurrent_path += "|" + root[i].name + "%";
                    recursive_skel += tcurrent_path;
                    if (root[i].child_nodes != null)
                    {
                        tcurrent_path = tcurrent_path.Substring(0, tcurrent_path.Length - 1);
                        recursive_traverse_skeleton(tcurrent_path, ref recursive_skel, root[i].child_nodes);
                    }
                    tcurrent_path = "";
                }
            }

            public byte[] tso_data()
            {
                return scene_skeleton_to_tso();
            }

            public scene()
            {
            }
        }

        public class material
        {
            public string name;
            public material()
            {
            }
            //other stuff is not needed now
        }

        public class mesh
        {
            public string name;
            public Single[] transform_matrix; //16 entries
            public UInt32 unknown1;
            public UInt32 sub_mesh_count;
            public UInt32 unknown3;
            public UInt32 bone_index_LUT_entry_count;
            public UInt32[] bone_index_LUT; //to look up bone field entries... bones are not directly assigned to vertices but by the means of this bone index LUT (look up table)... so if there is e.g. a bone field entry with the value 1, this means to look up in the LUT the first entry to retrieve the actual bone index...
            public UInt32 vertex_count;
            public vertex_field[] vertices;
            public mesh()
            {
            }
        }

        public class vertex_field
        {
            public Single[] position; //X,Y,Z
            public Single[] normal; //NX,NY,NZ
            public Single[] UV; //U,V
            public UInt32 bone_weight_entry_count;
            public List<bone_weight> bone_weight_field;
            public vertex_field()
            {
            }
            public vertex_field(vertex_field v)
            {
                //make a deep copy
                this.position = new Single[3];
                this.normal = new Single[3];
                this.UV = new Single[2];

                this.position[0] = v.position[0];
                this.position[1] = v.position[1];
                this.position[2] = v.position[2];

                this.normal[0] = v.normal[0];
                this.normal[1] = v.normal[1];
                this.normal[2] = v.normal[2];

                this.UV[0] = v.UV[0];
                this.UV[1] = v.UV[1];

                this.bone_weight_entry_count = v.bone_weight_entry_count;
                this.bone_weight_field = new List<bone_weight>();
                for (int i = 0; i < this.bone_weight_entry_count; i++)
                {
                    bone_weight new_bone_weight = new bone_weight();
                    new_bone_weight.bone_index = v.bone_weight_field[i].bone_index;
                    new_bone_weight.vertex_bone_weight = v.bone_weight_field[i].vertex_bone_weight;
                    this.bone_weight_field.Add(new_bone_weight);
                }
            }
        }
        public class bone_weight
        {
            public UInt32 bone_index;
            public Single vertex_bone_weight;
            public bone_weight()
            {
            }
        }

        public struct vertex_pos_map
        {
            public int mapped_index;
            public int new_position;
            public int unique_entry;
        }

        public class script
        {
            public string file_name;
            public string[] script_data;
            public script[] sub_scripts;
            public script()
            {
            }
        }

        public class texture
        {
            public string file_path; //usually the unquoted part of the entry
            public string file_name; //usually the name with quotations wrapped around it
            public byte[] data_stream;
            public texture()
            {
            }
        }

        public class bone_node
        {
            public string name;
            public Single[] transformation_matrix; //16 entries
            public bone_node[] child_nodes;
            public bone_node()
            {
            }
        }

        private static char[] spaceDelimiters = new char[] { '\t', ' ', '{' };
        private static char[] entryDelimiters = new char[] { '\t', ' ', ',', ';', '\"' };
        private static char[] sectionDelimiters = new char[] { '\t', ' ', '{', '}' };

        //might not work if there this triangle is coplanar to the z-plane
        public bool is_triangle_clockwise(Single[][] triangle)
        {
            int n = 3;                      /* Number of vertices */
            Single area;
            int i;

            area = triangle[n - 1][0] * triangle[0][1] - triangle[0][0] * triangle[n - 1][1];

            for (i = 0; i < n - 1; i++)
            {
                area += triangle[i][0] * triangle[i + 1][1] - triangle[i + 1][0] * triangle[i][1];
            }

            bool CW = false;
            if (area >= 0.0)
            {
                CW = false;
            }
            else
            {
                CW = true;
            }
            return CW;
        }

        public int encrypt_TSO(string source_path)
        {
            System.Console.Out.WriteLine("Genrating TSO from selected directory...");

            int ret = 0;
            use_mesh_binary = false;
            string[] allfiles = get_all_files_from_source_path(source_path);

            //now sorting allfiles lexicographical
            Array.Sort(allfiles);

            scene tso_scene = new scene();

            for (int i = 0; i < allfiles.Length; i++)
            {
                if (allfiles[i].Length > 4)
                {
                    if (allfiles[i].Substring(allfiles[i].Length - 4, 4).ToLower().Equals(".bmp"))
                    {
                        //this file is a bmp texture
                        add_BMP_texture(allfiles[i], source_path, ref tso_scene);
                    }
                    else if (allfiles[i].Substring(allfiles[i].Length - 4, 4).ToLower().Equals(".tga"))
                    {
                        //this file is a tga texture
                        add_TGA_texture(allfiles[i], source_path, ref tso_scene);
                    }
                    else if (allfiles[i].Substring(allfiles[i].Length - 2, 2).ToLower().Equals(".x"))
                    {
                        //this file is a direct-x-file
                        process_X_File(allfiles[i], source_path, ref tso_scene);
                    }
                    else if (allfiles[i].Substring(allfiles[i].Length - 5, 5).ToLower().Equals(".cgfx"))
                    {
                        //this file is a shader script. And more precisely the main shader file.
                        System.IO.StreamReader text_reader = new System.IO.StreamReader(System.IO.File.OpenRead(source_path + allfiles[i]), System.Text.Encoding.ASCII);
                        allfiles[i] = "\\" + allfiles[i].Substring(5);
                        script main_script = new script();
                        main_script.file_name = allfiles[i];
                        //string[] parse_rel_path = allfiles[i].Split(new string[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);
                        //string file_name = parse_rel_path[0];
                        //byte[] file_name_byte = System.Text.Encoding.ASCII.GetBytes(file_name);
                        System.Collections.ArrayList read_in = new System.Collections.ArrayList();
                        while (!text_reader.EndOfStream)
                        {
                            read_in.Add((string)text_reader.ReadLine());
                        }
                        main_script.script_data = (string[])read_in.ToArray("".GetType());
                        tso_scene.scripts = new script[1];
                        tso_scene.scripts[0] = main_script;
                        text_reader.Close();
                    }
                    else if (allfiles[i].Substring(allfiles[i].Length - 4, 4).ToLower().Equals(".bin"))
                    {
                        tso_scene.file_name_bin_mesh = source_path + allfiles[i];
                    }
                    else
                    {
                        //the file is either a sub shader or an unknown texture... this can be checked by
                        //probing the file header. If it is an unknown texture it should start with "BM"
                        try
                        {
                            reader = new System.IO.BinaryReader(System.IO.File.OpenRead(source_path + allfiles[i]));
                            byte[] magic_header = reader.ReadBytes(2);
                            reader.Close();
                            if ((magic_header[0] == 0x42) && (magic_header[1] == 0x4D))
                            {
                                //its an unknown texture
                                add_BMP_texture(allfiles[i], source_path, ref tso_scene);
                            }
                            else
                            {
                                //its a subshader
                                add_SubScript(allfiles[i], source_path, ref tso_scene);
                            }
                        }
                        catch (Exception e)
                        {
                            System.Console.Out.WriteLine("An exception occured:\r\n");
                            System.Console.Out.WriteLine(e.ToString());
                            System.Console.Out.WriteLine("Proceeding and assuming file was a shader.");
                            //assume its a subshader
                            add_SubScript(allfiles[i], source_path, ref tso_scene);
                        }
                    }
                }
                else
                {
                    //strange file... check header for bmp
                    //otherwise assume a shader script
                    try
                    {
                        reader = new System.IO.BinaryReader(System.IO.File.OpenRead(source_path + allfiles[i]));
                        byte[] magic_header = reader.ReadBytes(2);
                        reader.Close();
                        if ((magic_header[0] == 0x42) && (magic_header[1] == 0x4D))
                        {
                            //its an unknown texture
                            add_BMP_texture(allfiles[i], source_path, ref tso_scene);
                        }
                        else
                        {
                            //its a subshader
                            add_SubScript(allfiles[i], source_path, ref tso_scene);
                        }
                    }
                    catch (Exception e)
                    {
                        System.Console.Out.WriteLine("An exception occured:\r\n");
                        System.Console.Out.WriteLine(e.ToString());
                        System.Console.Out.WriteLine("Proceeding and assuming file was a shader.");
                        //assume its a subshader
                        add_SubScript(allfiles[i], source_path, ref tso_scene);
                    }
                }
            }
            //now the tso_scene should be complete...
            //it needs just to be written to a file now

            string file_name = source_path + ".tso";
            write_scene_to_file(file_name, ref tso_scene);
            System.Console.Out.WriteLine("DONE!");

            return ret;
        }

        public void flatten_skelleton(bone_node[] skelleton, ref UInt32 offset, ref bone_node[] skelleton_flat)
        {
            for (int i = 0; i < skelleton.Length; i++)
            {
                skelleton_flat[offset] = skelleton[i];
                offset++;
                if (skelleton[i].child_nodes != null)
                {
                    flatten_skelleton(skelleton[i].child_nodes, ref offset, ref skelleton_flat);
                }
            }
        }

        public void ParseSkipSection(System.IO.StreamReader text_reader, ref string line)
        {
            if (line.Contains("{") && !line.Contains("}"))
            {
                while (!(line = text_reader.ReadLine()).Contains("}"))
                {
                    if (line.Contains("{"))
                    {
                        ParseSkipSection(text_reader, ref line);
                    }
                }
            }
        }

        private void SkipToEndOfSection(System.IO.StreamReader text_reader, ref string line)
        {
            while (!(line = text_reader.ReadLine()).Contains("}"))
            {
                ParseSkipSection(text_reader, ref line);
            }
        }

        private void ParseFrame(System.IO.StreamReader text_reader, bone_node parent, ref bone_node[] skelletons, ref string line, List<string> texturePaths, ref scene tso_scene, string filename)
        {

            bone_node frame = new bone_node();
            mesh new_mesh = new mesh(); //may not be used, but could be used, if a mesh frame is read
            List<int[]> mesh_faces = new List<int[]>(); //when this is a Max export, the face list must be used to retrieve the corrrect vertex lists. 
            float[] backup_matrix;
            List<string> bone_names = new List<string>();
            List<uint> bone_indices = new List<uint>();

            string[] substrings = line.Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries);
            frame.name = substrings[1];
            string fname = frame.name;

            string file_name = filename.Substring(filename.LastIndexOf("\\") + 1);
            file_name = file_name.Substring(0, file_name.Length - 2);
            file_name = file_name.ToLower();
            if (fname.Equals("Frame_World"))
            {
                //Okino Polytrans standard name
                return;
            }
            else if (fname.Equals("Frame_default"))
            {
                //Okino Polytrans standard name
                return;
            }
            else if (fname.Contains("Frame_f_") && fname.Substring(fname.Length - 2, 2).Equals("_0"))
            {
                //Okino Polytrans standard name
                return;
            }
            else if (fname.Contains(file_name))
            {
                //Okino Polytrans standard name
                return;
            }

            //3DS Max Frame prefilter
            fname = fname.Replace("Frame_", "");
            fname = fname.Replace("Anim_MatrixFrame_", "");
            if (fname.Substring(fname.Length - 2).Equals("_0"))
            {
                fname = fname.Substring(0, fname.Length - 2);
            }
            //end 3DS Max Frame prefilter
            //fname = fname.Replace("BONE_", "");
            frame.name = fname;


            if (fname.Contains("_sep_"))
            {
                //This frame contains mesh data, that is supposed to be
                //located in the frame encoded into this frame name here

                //since this contains bone data too, one needs to check if the mesh is aligned to world space
                //(a common alignment by used in 3ds max for most but not all bones), or like it should always
                //be, aligned to the actual frame thats encoded in the name...

                //If the Bone is ecnoded as child of the world frame, assume its transformation matrix to be
                //the identity matrix in world space. Or if you prefer this, to each Bone exists a frame that
                //contains an inverse matrix to the current bone's matrix in world space, multiplying these two
                //should give the identity matrix as a result.
                //But there is more... the path in the hierarchy that leads to the actual place of the Bone/Mesh
                //possibly contains transformation matrices, these must be applied (multiplied) on the assumed
                //identity matrix in order to realign the bone in hierarchy structure.
                //The boolean use3dsMaxWorkaround is set to true if the mesh/bone is not on the correct place.

                if (fname.Contains("M_E_S_H"))
                {
                    // a mesh frame
                    string[] separatorFrameName = new string[1];
                    separatorFrameName[0] = "_sep_";
                    string[] name_parts = fname.Split(separatorFrameName, StringSplitOptions.RemoveEmptyEntries);
                    new_mesh.unknown1 = System.UInt32.Parse(name_parts[2]);
                    new_mesh.sub_mesh_count = System.UInt32.Parse(name_parts[3]);
                    new_mesh.unknown3 = System.UInt32.Parse(name_parts[4]);
                    string name = name_parts[0].Substring(6);
                    new_mesh.name = name.Replace("_colon_", ":");
                    //okay... now add new_mesh to the referenced mesh list
                    if (tso_scene.meshes == null)
                    {
                        tso_scene.meshes = new List<mesh>();
                    }
                    tso_scene.meshes.Add(new_mesh);
                }
                else if(frame.name.Contains("BONE_"))
                {
                    //a skelletons frame
                    //adding one to bone_node_count
                    frame.name = frame.name.Replace("BONE_", "");
                    tso_scene.bone_node_count++;
                    string[] separatorFrameName = new string[1];
                    separatorFrameName[0] = "_sep_";
                    string[] parentFrame = fname.Split(separatorFrameName, StringSplitOptions.RemoveEmptyEntries);
                    if (skelletons == null)
                    {
                        System.Console.Out.WriteLine("Error: The mesh files are located before the bone/frame structure in the x-File, this parser cannot yet process such x-Files.");
                        return;
                    }
                    //now the first entry should be Frame_, therefore ignore entry at 0
                    //The last entry is usually the mesh name, which does not matter either.

                    frame = skelletons[skelletons.Length - 1];
                    bone_node compareFrame = parent;
                    try
                    {
                        for (int i = parentFrame.Length - 2; i > 0; i--)
                        {
                            if (compareFrame.name.Equals(parentFrame[i]))
                            {
                                if (i != 0)
                                {
                                    bone_node[] traversal_path = get_path_traversal_bone_nodes(skelletons, compareFrame);
                                    compareFrame = traversal_path[traversal_path.Length - 2];
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        //assume world aligned (means parent frame not accessible)
                    }

                    for (int i = 0; i < parentFrame.Length - 1; i++)
                    {
                        if (frame.child_nodes == null)
                        {
                        }
                        else
                        {
                            for (int j = 0; j < frame.child_nodes.Length; j++)
                            {
                                string tfname = frame.child_nodes[j].name;
                                if (tfname.Equals(parentFrame[i]))
                                {
                                    frame = frame.child_nodes[j];
                                    break;
                                }
                            }
                        }
                    }
                    backup_matrix = frame.transformation_matrix;
                }
            }
            else if(frame.name.Contains("BONE_"))
            {
                //such frames must be root frames
                frame.name = frame.name.Replace("BONE_", "");
                tso_scene.bone_node_count++;
                if (parent.name != null)
                {
                    if (parent.child_nodes == null)
                    {
                        parent.child_nodes = new bone_node[1];
                        parent.child_nodes[0] = frame;
                    }
                    else
                    {
                        int child_cnt = parent.child_nodes.Length;
                        bone_node[] new_child_node_set = new bone_node[child_cnt + 1];
                        for (int o = 0; o < child_cnt; o++)
                        {
                            new_child_node_set[o] = parent.child_nodes[o];
                        }
                        new_child_node_set[child_cnt] = frame;
                        parent.child_nodes = new_child_node_set;
                    }
                }
                if (parent.name == null)
                {
                    if (skelletons == null)
                    {
                        skelletons = new bone_node[1];
                        skelletons[0] = frame;
                    }
                    else
                    {
                        int skelletons_count = skelletons.Length;
                        bone_node[] new_skelletons_set = new bone_node[skelletons_count + 1];
                        for (int o = 0; o < skelletons_count; o++)
                        {
                            new_skelletons_set[o] = skelletons[o];
                        }
                        new_skelletons_set[skelletons_count] = frame;
                        skelletons = new_skelletons_set;
                    }
                }
            }

            while (!(line = text_reader.ReadLine()).Contains("}"))
            {
                if (line.Contains("FrameTransformMatrix"))
                {
                    List<float> floats = new List<float>();
                    while (floats.Count < 16)
                    {
                        while ((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length <= 0) ;
                        substrings = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < substrings.Length; i++)
                        {
                            floats.Add(Single.Parse(substrings[i], Culture));
                        }
                    }
                    frame.transformation_matrix = floats.ToArray();
                    if (frame.name.Contains("M_E_S_H"))
                    {
                        new_mesh.transform_matrix = floats.ToArray();
                    }

                    SkipToEndOfSection(text_reader, ref line);
                }
                else if (line.Contains("Mesh "))
                {
                    List<int> mat_index_entries = new List<int>(); //only used when submesh materials are present
                    List<string> sub_meshes = new List<string>(); //only used when submesh materials are present
                    List<int> mesh_vertex_borders = new List<int>(); //only used when submesh materials are present
                    List<int> mesh_vertex_borders_idx = new List<int>(); //only used when submesh materials are present
                    //bool non_modded_x_file = false;

                    string[] sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                    string name = sArray[1].Replace("_colon_", ":");
                    if (name.Contains("_sub_") && name.Substring(name.Length - 2, 2).Equals("_0"))
                    {
                        name = name.Substring(0, name.Length - 2);
                    }
                    //now find the mesh with this name...
                    int index_pos = -1;
                    for (int i = 0; i < tso_scene.meshes.ToArray().Length; i++)
                    {
                        if (tso_scene.meshes[i].name.Equals(name))
                        {
                            index_pos = i;
                            break;
                        }
                        else if (name.Length < 3)
                        {
                            //do nothing...
                        }
                        else if (name.Substring(name.Length - 2, 2).Equals("_0"))
                        {
                            //bah Polytrans again...
                            if (tso_scene.meshes[i].name.Equals(name.Substring(0, name.Length - 2)))
                            {
                                index_pos = i;
                                break;
                            }
                        }
                    }
                    if (index_pos == -1)
                    {
                        //no such mesh found
                        System.Console.Out.WriteLine("There was a mesh ID but no mesh frame for it. Discarded this mesh. TSO likely incomplete.");
                    }

                    while ((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length <= 0) ;
                    sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                    int numVertices = Int32.Parse(sArray[0]);
                    tso_scene.meshes[index_pos].vertex_count = (uint)numVertices;
                    tso_scene.meshes[index_pos].vertices = new vertex_field[numVertices];
                    for (int i = 0; i < numVertices; i++)
                    {
                        line = text_reader.ReadLine();
                        sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                        tso_scene.meshes[index_pos].vertices[i] = new vertex_field();
                        tso_scene.meshes[index_pos].vertices[i].position = new float[] { Single.Parse(sArray[0], Culture), Single.Parse(sArray[1], Culture), Single.Parse(sArray[2], Culture) };
                    }

                    //!MOD_TSO

                    /*while ((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length <= 0) ;
                    if (line.Contains("//!MOD_TSO"))
                    {
                        non_modded_x_file = true;
                        List<vertex_field> new_verts = new List<vertex_field>();
                        sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                        vertex_field new_vertt = new vertex_field();
                        new_vertt.position = new float[] { Single.Parse(sArray[1], Culture), Single.Parse(sArray[2], Culture), Single.Parse(sArray[3], Culture) };
                        new_verts.Add(new_vertt);
                        while ((line = text_reader.ReadLine()).Contains("//!MOD_TSO"))
                        {
                            sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                            vertex_field new_vert = new vertex_field();
                            new_vert.position = new float[] { Single.Parse(sArray[1], Culture), Single.Parse(sArray[2], Culture), Single.Parse(sArray[3], Culture) };
                            new_verts.Add(new_vert);
                        }
                        tso_scene.meshes[index_pos].vertices = new_verts.ToArray();
                        tso_scene.meshes[index_pos].vertex_count = (UInt32)tso_scene.meshes[index_pos].vertices.Length;
                    }*/
                    while ((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length <= 0) ;
                    sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                    int numFaces = Int32.Parse(sArray[0]);
                    for (int i = 0; i < numFaces; i++)
                    {
                        line = text_reader.ReadLine();
                        sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                        mesh_faces.Add(new int[] { Int32.Parse(sArray[1]), Int32.Parse(sArray[2]), Int32.Parse(sArray[3]) });
                    }

                    while (!(line = text_reader.ReadLine()).Contains("}"))
                    {
                        if (line.Contains("MeshNormals"))
                        {
                            /*if (non_modded_x_file)
                            {
                                while ((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length <= 0) ;
                                sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                int numNormals = Int32.Parse(sArray[0]);
                                for (int i = 0; i < numNormals; i++)
                                {
                                    line = text_reader.ReadLine();
                                }
                                int pos = 0;
                                while (((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length <= 0) && !line.Contains("//!MOD_TSO")) ;
                                sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                tso_scene.meshes[index_pos].vertices[pos].normal = new float[] { Single.Parse(sArray[1], Culture), Single.Parse(sArray[2], Culture), Single.Parse(sArray[3], Culture) };
                                pos++;
                                while (((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length > 0 ) && line.Contains("//!MOD_TSO"))
                                {
                                    sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                    tso_scene.meshes[index_pos].vertices[pos].normal = new float[] { Single.Parse(sArray[1], Culture), Single.Parse(sArray[2], Culture), Single.Parse(sArray[3], Culture) };
                                    pos++;
                                }
                                SkipToEndOfSection(text_reader, ref line);
                            }
                            else
                            {*/
                                while ((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length <= 0) ;
                                sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                int numNormals = Int32.Parse(sArray[0]);
                                for (int i = 0; i < numNormals; i++)
                                {
                                    line = text_reader.ReadLine();
                                    sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                    tso_scene.meshes[index_pos].vertices[i].normal = new float[] { Single.Parse(sArray[0], Culture), Single.Parse(sArray[1], Culture), Single.Parse(sArray[2], Culture) };
                                }
                                SkipToEndOfSection(text_reader, ref line);
                            //}
                        }
                        else if (line.Contains("MeshTextureCoords"))
                        {
                            while ((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length <= 0) ;
                            sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                            int numUVs = Int32.Parse(sArray[0]);
                            /*if (non_modded_x_file)
                            {
                                for (int i = 0; i < numUVs; i++)
                                {
                                    line = text_reader.ReadLine();
                                }
                                int pos = 0;
                                while (((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length <= 0) && !line.Contains("//!MOD_TSO")) ;
                                sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                tso_scene.meshes[index_pos].vertices[pos].UV = new float[] { Single.Parse(sArray[1], Culture), Single.Parse(sArray[2], Culture)};
                                pos++;
                                while (((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length > 0) && line.Contains("//!MOD_TSO"))
                                {
                                    sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                    tso_scene.meshes[index_pos].vertices[pos].UV = new float[] { Single.Parse(sArray[1], Culture), Single.Parse(sArray[2], Culture)};
                                    pos++;
                                }
                            }
                            else
                            {*/
                                for (int i = 0; i < numUVs; i++)
                                {
                                    line = text_reader.ReadLine();
                                    sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                    tso_scene.meshes[index_pos].vertices[i].UV = new float[] { Single.Parse(sArray[0], Culture), Single.Parse(sArray[1], Culture) };
                                }
                                SkipToEndOfSection(text_reader, ref line);
                            //}
                        }
                        else if (line.Contains("MeshMaterialList "))
                        {
                            while ((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length <= 0) ;
                            //could also be done in one line... some parsers do so.
                            sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                            if (sArray.Length > 1)
                            {
                                //all done in one line...
                                mat_index_entries.Add(Int32.Parse(sArray[2]));
                            }
                            else
                            {
                                while ((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length <= 0) ;
                                while (((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length > 0) &&
                                    mat_index_entries.Count < mesh_faces.Count)
                                {
                                    sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                    if (sArray.Length > 1)
                                    {
                                        for (int j = 0; j < sArray.Length; j++)
                                        {
                                            mat_index_entries.Add(Int32.Parse(sArray[j]));
                                        }
                                    }
                                    else
                                    {
                                        mat_index_entries.Add(Int32.Parse(sArray[0]));
                                    }
                                }
                            }
                            //okay if available, this might encode submeshes...

                            string mat_name = "";
                            if ((line.Contains("{")) && line.Contains("}"))
                            {
                                //okay check if the material includes M_A_T
                                mat_name = line.Substring(line.IndexOf("{") + 1, line.Length - line.IndexOf("{") - 2);
                            }
                            if (mat_name.Contains("M_A_T"))
                            {
                                //part of a multi mesh
                                sub_meshes.Add(mat_name);
                            }

                            while (!((line = text_reader.ReadLine()).Contains("}") && !(line.Contains("{"))))
                            {
                                if (line.Contains("MeshBorders"))
                                {
                                    
                                    sArray = line.Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                    sArray[2] = sArray[2].Replace(";", "");
                                    mesh_vertex_borders.Add(Int32.Parse(sArray[2]));
                                }
                                //some directx parser take a short cut and write just the name of the material in {}
                                mat_name = "";
                                if ((line.Contains("{")) && line.Contains("}"))
                                {
                                    //okay check if the material includes M_A_T
                                    mat_name = line.Substring(line.IndexOf("{") + 1, line.Length - line.IndexOf("{") - 2);
                                }
                                if (line.Contains("Material "))
                                {
                                    material new_material = new material();
                                    new_material.name = line.Substring(line.IndexOf("Material ") + 9).Replace(" ", "").Replace("{", "");
                                    if (tso_scene.mat_list == null)
                                    {
                                        tso_scene.mat_list = new List<material>();
                                    }
                                    tso_scene.mat_list.Add(new_material);
                                    mat_name = new_material.name;

                                    //the following material related stuff is just for x-file compatibility, its not used in a TSO file in a mesh. Materials are defined in shader scripts
                                    while ((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length <= 0) ;
                                    sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                    float[] ambient = new float[] { Single.Parse(sArray[0], Culture), Single.Parse(sArray[1], Culture), Single.Parse(sArray[2], Culture), Single.Parse(sArray[3], Culture) };
                                    line = text_reader.ReadLine();
                                    sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                    float specularPower = Single.Parse(sArray[0], Culture);
                                    line = text_reader.ReadLine();
                                    sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                    float[] specular = new float[] { Single.Parse(sArray[0], Culture), Single.Parse(sArray[1], Culture), Single.Parse(sArray[2], Culture), 1f };
                                    line = text_reader.ReadLine();
                                    sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                    float[] emissive = new float[] { Single.Parse(sArray[0], Culture), Single.Parse(sArray[1], Culture), Single.Parse(sArray[2], Culture), 1f };
                                    List<string> materialTextures = new List<string>();

                                    while (!(line = text_reader.ReadLine()).Contains("}"))
                                    {
                                        if (line.Contains("TextureFilename"))
                                        {
                                            while ((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length <= 0) ;
                                            sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                            if (!texturePaths.Contains(sArray[0]))
                                            {
                                                texturePaths.Add(sArray[0]);
                                            }

                                            if (line.Contains("{"))
                                            {
                                                ParseSkipSection(text_reader, ref line);
                                            }
                                            SkipToEndOfSection(text_reader, ref line);
                                        }
                                        else
                                        {
                                            ParseSkipSection(text_reader, ref line);
                                        }
                                    }

                                }
                                else
                                {
                                    ParseSkipSection(text_reader, ref line);
                                }

                                //now this is important...
                                if (mat_name.Contains("M_A_T"))
                                {
                                    //part of a multi mesh
                                    sub_meshes.Add(mat_name);
                                }

                            }
                            //if there are sub meshes process them later... (after vertex field correction)
                        }
                        else if (line.Contains("SkinWeights"))
                        {
                            //This is importent it deals with bone weights... the important question is which ones...
                            while ((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length <= 0) ;
                            sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                            string boneName = sArray[0];
                            //3DS Max correction
                            if (!boneName.Contains("BONE_"))
                            {
                                //this is not an original bone... its a useless Polytrans bone
                                System.Console.WriteLine("Warning: Discarding useless Polytrans bone: " + boneName + ".");
                                TSOdecrypt.Program.show_warnings = true;
                            }
                            else
                            {
                                boneName = boneName.Replace("Frame_", "");
                                boneName = boneName.Replace("Anim_MatrixFrame_", "");
                                boneName = boneName.Replace("BONE_", "");
                                List<float> CopyWeights = new List<float>();
                                //3DS Max correction end
                                string[] delimName = new string[1];
                                delimName[0] = "_sep_";
                                sArray = boneName.Split(delimName, StringSplitOptions.RemoveEmptyEntries);
                                if (sArray.Length > 1)
                                {
                                    boneName = sArray[sArray.Length - 2];
                                }
                                else
                                {
                                    boneName = sArray[0];
                                    //Another 3DS Max .x export fix
                                    if (boneName.Substring(boneName.Length - 2).Equals("_0"))
                                    {
                                        boneName = boneName.Substring(0, boneName.Length - 2);
                                    }
                                }
                                //now every bone_name has an index in a collapsed root skelleton field. This index will be important for
                                //the bone_index_LUT
                                boneName = boneName.Replace("\"", "");
                                bone_names.Add(boneName);
                                //now retrieve its absolute index in the flattened bone_index field of the root skelleton
                                //the only problem here is, that one must know the bone_node count beforehand to initialize
                                //the flattened array, thats no problem since we have this value stored in the tso_scene
                                bone_node[] flat_root = new bone_node[tso_scene.bone_node_count];
                                uint offset = 0; //initialize always with 0!!
                                flatten_skelleton(skelletons, ref offset, ref flat_root);
                                //now get the index of the bone_node with the name of boneName in the flat_root array
                                for (int o = 0; o < flat_root.Length; o++)
                                {
                                    if (flat_root[o].name.Equals(boneName))
                                    {
                                        bone_indices.Add((UInt32)o);
                                        break;
                                    }
                                    else if (o == flat_root.Length - 1)
                                    {
                                        System.Console.Out.WriteLine("Warning: bone not found in skeleton, wrong skinning possible.");
                                        TSOdecrypt.Program.show_warnings = true;
                                    }
                                }

                                line = text_reader.ReadLine();
                                sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                int numWeights = Int32.Parse(sArray[0]);
                                //first a field of indices...
                                int[] vertexIndexes = new int[numWeights];
                                List<float> bone_weights_all = new List<float>();
                                /*if (non_modded_x_file)
                                {
                                    List<int> new_vertIndexes = new List<int>();
                                    for (int i = 0; i < numWeights; i++)
                                    {
                                        line = text_reader.ReadLine();
                                    }
                                    int pos = 0;
                                    while (((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length <= 0) && !line.Contains("//!MOD_TSO")) ;
                                    sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                    new_vertIndexes.Add(Int32.Parse(sArray[1]) );
                                    pos++;
                                    while (((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length > 0) && line.Contains("//!MOD_TSO"))
                                    {
                                        sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                        new_vertIndexes.Add(Int32.Parse(sArray[1]));
                                        pos++;
                                    }
                                    vertexIndexes = new_vertIndexes.ToArray();
                                    for (int i = 0; i < numWeights-1; i++)
                                    {
                                        line = text_reader.ReadLine();
                                    }
                                    pos = 0;
                                    while (((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length <= 0) && !line.Contains("//!MOD_TSO")) ;
                                    sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                    bone_weights_all.Add(Single.Parse(sArray[1], Culture));
                                    pos++;
                                    while (((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length > 0) && line.Contains("//!MOD_TSO"))
                                    {
                                        sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                        bone_weights_all.Add(Single.Parse(sArray[1], Culture));
                                        pos++;
                                    }
                                    numWeights = pos;
                                }
                                else
                                {*/
                                    for (int i = 0; i < numWeights; i++)
                                    {
                                        line = text_reader.ReadLine();
                                        sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                        for (int j = 0; j < sArray.Length; j++)
                                        {
                                            vertexIndexes[i + j] = Int32.Parse(sArray[j]);
                                        }
                                        i += sArray.Length - 1;
                                    }
                                    for (int i = 0; i < numWeights; i++)
                                    {
                                        line = text_reader.ReadLine();
                                        sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                        for (int j = 0; j < sArray.Length; j++)
                                        {
                                            bone_weights_all.Add(Single.Parse(sArray[j], Culture));
                                        }
                                        i += sArray.Length - 1;
                                    }
                                //}
                                List<float> boneFloats = new List<float>();
                                while (boneFloats.Count < 16)
                                {
                                    while ((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length <= 0) ;
                                    substrings = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                    for (int i = 0; i < substrings.Length; i++)
                                    {
                                        boneFloats.Add(Single.Parse(substrings[i], Culture));
                                    }
                                }
                                for (int i = 0; i < numWeights; i++)
                                {
                                    bone_weight new_bone_weight = new bone_weight();
                                    new_bone_weight.bone_index = (UInt32)(bone_indices.Count-1);
                                    new_bone_weight.vertex_bone_weight = bone_weights_all[i];
                                    if (tso_scene.meshes[index_pos].vertices.Length > vertexIndexes[i])
                                    {
                                        if (tso_scene.meshes[index_pos].vertices[vertexIndexes[i]].bone_weight_field == null)
                                        {
                                            tso_scene.meshes[index_pos].vertices[vertexIndexes[i]].bone_weight_field = new List<bone_weight>();
                                        }
                                        tso_scene.meshes[index_pos].vertices[vertexIndexes[i]].bone_weight_field.Add(new_bone_weight);
                                        tso_scene.meshes[index_pos].vertices[vertexIndexes[i]].bone_weight_entry_count++;
                                    }
                                }
                            }
                            //boneFloats holds now the transformation matrix for that node. That
                            //is important for the x-file. But has no impact on the TSO file.
                            //Thus it is ignored.

                            //3DS Max adds dummy bones that give vertices a minimum weight of 0.001
                            //These dummy bones are pretty much useless. They can be recognized by
                            //their identity matrix and the name is equal to the frame name
                            //they should be filtered before this whole method actually starts...
                            //atm the user must remove them himself.

                            //okay here the skin weights for that bone_node are applied... if there are more
                            //they will be dealt with in further loops.

                            SkipToEndOfSection(text_reader, ref line);
                        }
                        else
                        {
                            ParseSkipSection(text_reader, ref line);
                        }
                    }
                    //end of skin weights... and the other stuff
                    //remember... bone_names and bone_indices are not yet in tso_scene.meshes[index_pos]
                    //but now is the right time to bring them in.
                    List<vertex_field> new_vert_field = new List<vertex_field>();
                    tso_scene.meshes[index_pos].bone_index_LUT = bone_indices.ToArray();
                    tso_scene.meshes[index_pos].bone_index_LUT_entry_count = (UInt32)bone_indices.ToArray().Length;

                    //sorting mesh_faces array and mat_index_entries when mat_index_entries.Count > 0

                    if (mat_index_entries.Count > 1)
                    {
                        int[] t_mat_index_entries = mat_index_entries.ToArray();
                        int[][] t_mesh_faces = mesh_faces.ToArray();
                        Array.Sort(t_mat_index_entries, t_mesh_faces);
                        mesh_faces = t_mesh_faces.ToList<int[]>();
                        mat_index_entries = t_mat_index_entries.ToList<int>();
                    }

                    /*if (non_modded_x_file)
                    {
                        if (mesh_vertex_borders.Count > 0)
                        {
                            int add_all_subs = 0;
                            for (int i = 1; i < mesh_vertex_borders.Count; i++)
                            {
                                add_all_subs += mesh_vertex_borders[i];
                            }
                            mesh_vertex_borders[0] -= add_all_subs;
                            add_all_subs = 0;
                            for (int i = 0; i < mesh_vertex_borders.Count; i++)
                            {
                                add_all_subs += mesh_vertex_borders[i];
                                mesh_vertex_borders[i] = add_all_subs;
                            }
                            mesh_vertex_borders.RemoveAt(mesh_vertex_borders.Count - 1);
                        }
                        else
                        {
                            mesh_vertex_borders.Add((int)tso_scene.meshes[index_pos].vertex_count);
                        }
                        for (int i = 0; i < tso_scene.meshes[index_pos].vertex_count; i++)
                        {
                            new_vert_field.Add(new vertex_field(tso_scene.meshes[index_pos].vertices[i]));
                        }
                    }
                    else
                    {*/
                        //minimizing vertices list 
                        //erase vertex duplicates from vertex list...
                        List<vertex_pos_map> vertex_positions_map = new List<vertex_pos_map>();
                        int new_pos = 0;
                        for (int j = 0; j < tso_scene.meshes[index_pos].vertex_count; j++)
                        {
                            bool is_in_list = false;
                            for (int h = 0; h < vertex_positions_map.Count; h++)
                            {
                                if (vertex_positions_map[h].unique_entry == 1)
                                {
                                    if ((tso_scene.meshes[index_pos].vertices[(vertex_positions_map[h]).mapped_index].position[0].Equals(tso_scene.meshes[index_pos].vertices[j].position[0])) &&
                                         (tso_scene.meshes[index_pos].vertices[(vertex_positions_map[h]).mapped_index].position[1].Equals(tso_scene.meshes[index_pos].vertices[j].position[1])) &&
                                         (tso_scene.meshes[index_pos].vertices[(vertex_positions_map[h]).mapped_index].position[2].Equals(tso_scene.meshes[index_pos].vertices[j].position[2])))
                                    {
                                        //now testing if bone weights are identical
                                        if ((tso_scene.meshes[index_pos].vertices[(vertex_positions_map[h]).mapped_index].bone_weight_entry_count.Equals(tso_scene.meshes[index_pos].vertices[j].bone_weight_entry_count)))
                                        {
                                            //same entry count for bone weights... but the actual bones can still be different...
                                            bool identical_bones = true;
                                            for (int k = 0; k < tso_scene.meshes[index_pos].vertices[j].bone_weight_entry_count; k++)
                                            {
                                                if (!(tso_scene.meshes[index_pos].vertices[(vertex_positions_map[h]).mapped_index].bone_weight_field[k].bone_index.Equals(tso_scene.meshes[index_pos].vertices[j].bone_weight_field[k].bone_index)))
                                                {
                                                    identical_bones = false;
                                                }
                                            }
                                            if (identical_bones)
                                            {
                                                //now testing for identical vertex normals and UVs
                                                bool equal_normal = true;
                                                bool equal_UVs = true;
                                                /*for (int k = 0; k < 3; k++)
                                                {
                                                    if (!(tso_scene.meshes[index_pos].vertices[(vertex_positions_map[h]).mapped_index].normal[k].Equals(tso_scene.meshes[index_pos].vertices[j].normal[k])))
                                                    {
                                                        equal_normal = false;
                                                        break;
                                                    }
                                                }*/
                                                if (!(tso_scene.meshes[index_pos].vertices[(vertex_positions_map[h]).mapped_index].UV[0].Equals(tso_scene.meshes[index_pos].vertices[j].UV[0])))
                                                {
                                                    equal_UVs = false;
                                                }
                                                if (!(tso_scene.meshes[index_pos].vertices[(vertex_positions_map[h]).mapped_index].UV[1].Equals(tso_scene.meshes[index_pos].vertices[j].UV[1])))
                                                {
                                                    equal_UVs = false;
                                                }
                                                if (equal_normal && equal_UVs)
                                                {
                                                    is_in_list = true;
                                                    vertex_pos_map new_entry = new vertex_pos_map();
                                                    new_entry.mapped_index = (vertex_positions_map[h]).mapped_index;
                                                    new_entry.new_position = vertex_positions_map[h].new_position;
                                                    new_entry.unique_entry = -1;
                                                    vertex_positions_map.Add(new_entry);
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            if (!is_in_list)
                            {
                                vertex_pos_map new_entry = new vertex_pos_map();
                                new_entry.mapped_index = j;
                                new_entry.new_position = new_pos;
                                new_entry.unique_entry = 1;
                                vertex_positions_map.Add(new_entry);
                                new_pos++;
                            }
                        }

                        //the vertices field must now be encoded for TSO files
                        int faces_cnt = mesh_faces.Count;

                        List<int[]> new_faces_list = new List<int[]>();
                        List<int> un_booked_faces = new List<int>();
                        List<string> mesh_names_copy = new List<string>(); //only used for multi mesh atm.

                        //now updating face list...
                        for (int i = 0; i < faces_cnt; i++)
                        {
                            for (int j = 0; j < 3; j++)
                            {
                                mesh_faces[i][j] = vertex_positions_map[mesh_faces[i][j]].new_position;
                            }
                        }
                        List<vertex_field> new_verts_field = new List<vertex_field>();
                        for (int i = 0; i < tso_scene.meshes[index_pos].vertex_count; i++)
                        {
                            if (vertex_positions_map[i].unique_entry == 1)
                            {
                                new_verts_field.Add(tso_scene.meshes[index_pos].vertices[i]);
                            }
                        }
                        tso_scene.meshes[index_pos].vertices = new_verts_field.ToArray();


                        int faces_lower_bound = 0;
                        int faces_upper_bound = 0;

                        List<int> lowerbounds = new List<int>();
                        List<int> upperbounds = new List<int>();
                        for (int i = 0; i < faces_cnt; i++)
                        {
                            if (i == 0)
                            {
                                if (mat_index_entries.Count > 1)
                                {
                                    for (int j = 1; (j < mat_index_entries.Count && mat_index_entries[j - 1] == mat_index_entries[j]); j++)
                                    {
                                        faces_upper_bound = j;
                                    }
                                    faces_upper_bound++;
                                    lowerbounds.Add(0);
                                    upperbounds.Add(faces_upper_bound);
                                    //mesh_names_copy.Add(sub_meshes[true_index]);
                                }
                                else
                                {
                                    faces_upper_bound = mesh_faces.Count;
                                    lowerbounds.Add(0);
                                    upperbounds.Add(faces_upper_bound);
                                }
                            }
                            if ((mat_index_entries.Count > 1) && (i != 0))
                            {
                                if (mat_index_entries[i - 1] != mat_index_entries[i])
                                {
                                    faces_lower_bound = i;
                                    faces_upper_bound = faces_lower_bound;
                                    for (int j = i + 1; (j < (mat_index_entries.Count - 1)) && (mat_index_entries[j - 1] == mat_index_entries[j]); j++)
                                    {
                                        faces_upper_bound = j;
                                    }
                                    faces_upper_bound++;
                                    if (faces_upper_bound == (mat_index_entries.Count -1))
                                    {
                                        faces_upper_bound++;
                                    }
                                    lowerbounds.Add(faces_lower_bound);
                                    upperbounds.Add(faces_upper_bound);
                                    /*true_index++;
                                    string old_name = sub_meshes[true_index];
                                    string new_name = old_name.Substring(0, old_name.IndexOf("_sub_") + 4) + "_" + mesh_names_copy.Count.ToString();
                                    old_name = old_name.Substring(old_name.IndexOf("_sub_") + 5);
                                    old_name = old_name.Substring(old_name.IndexOf("_sep_"));
                                    new_name += old_name;
                                    mesh_names_copy.Add(new_name);*/
                                }
                            }
                            /*if ((mat_index_entries.Count > 1) && (i - lowerbounds[lowerbounds.Count-1] > 2000))
                            {
                                //compart mesh here
                                lowerbounds.Add(i);
                                upperbounds.Add(upperbounds[upperbounds.Count-1]);
                                upperbounds[upperbounds.Count - 2] = i;
                                //now inserting new submesh name
                                if (mesh_names_copy.Count > 1)
                                {
                                    string old_name = mesh_names_copy[mesh_names_copy.Count - 1];
                                    string new_name = old_name.Substring(0, old_name.IndexOf("_sub_") + 4) + "_" + mesh_names_copy.Count.ToString();
                                    old_name = old_name.Substring(old_name.IndexOf("_sub_") + 5);
                                    old_name = old_name.Substring(old_name.IndexOf("_sep_"));
                                    new_name += old_name;
                                    mesh_names_copy.Add(new_name);
                                }
                                else
                                {
                                    string old_name = mesh_names_copy[mesh_names_copy.Count - 1];
                                    sArray = old_name.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                    sArray = sArray[0].Split(new string[] { "_sep_" }, StringSplitOptions.RemoveEmptyEntries);
                                    string new_name = sArray[0] + "_sub_" + mesh_names_copy.Count.ToString() + "_sep_" + sArray[1] + "_sep_" + sArray[2] + "_sep_" + sArray[3] + "_sep_" +sArray[4] + "_sep_";
                                    mesh_names_copy.Add(new_name);
                                }
                            }*/
                        }
                        /*if (mesh_names_copy.Count > 1)
                        {
                            sub_meshes = mesh_names_copy;
                        }*/
                        List<List<int[]>> mesh_faces_lst = new List<List<int[]>>();
                        for (int i = 0; i < lowerbounds.Count; i++)
                        {
                            mesh_faces_lst.Add(new List<int[]>());
                            for (int j = lowerbounds[i]; j < upperbounds[i]; j++)
                            {
                                mesh_faces_lst[i].Add(mesh_faces[j]);
                            }
                        }

                        List<Int32> vertex_list = null;
                        for (int i = 0; i < mesh_faces_lst.Count; i++)
                        {
                            vertex_list = new List<Int32>();
                            for (int a = 0; a < mesh_faces_lst[i].Count; a++)
                            {
                                for (int b = 2; b >= 0; b--)
                                {
                                    vertex_list.Add((Int32)mesh_faces_lst[i][a][b]);
                                }
                            }
                            /*Int32[] vertex_array = vertex_list.ToArray();
                            SetCacheSize(CACHESIZE_GEFORCE_3_4_5_6);
                            SetMinStripSize(2);
                            SetStitchStrips(true);
                            PrimitiveGroup[] primGroup = null;
                            //PrimitiveGroup[] primGroup = new PrimitiveGroup[0];
                            IntPtr ptr_to_primgroup = Marshal.AllocHGlobal(0);
                            IntPtr ptr_to_primgroup_size = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Int32)));
                            IntPtr ptr_to_index_field = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Int32)) * vertex_array.Length);
                            for(int t=0; t<vertex_array.Length; t++)
                            {
                                Marshal.WriteInt32((IntPtr)(ptr_to_index_field.ToInt64() + 4 * t), vertex_array[t]);
                            }
                            //primGroup[0] = new PrimitiveGroup(0);
                            Int32 group_cnt = 0;
                            bool Invalidate = false;
                            try
                            {
                                bool result = GenerateStrips(ptr_to_index_field, (Int32)vertex_array.Length, ptr_to_primgroup, ptr_to_primgroup_size, Invalidate);
                                group_cnt = Marshal.ReadInt32(ptr_to_primgroup_size);
                                Marshal.FreeHGlobal(ptr_to_primgroup_size);
                                Marshal.FreeHGlobal(ptr_to_index_field);
                                primGroup = new PrimitiveGroup[group_cnt];
                                IntPtr prim_group_ptr = Marshal.ReadIntPtr(ptr_to_primgroup);
                                unsafe
                                {
                                    for (int t = 0; t < group_cnt; t++)
                                    {
                                        primGroup[t] = (PrimitiveGroup)Marshal.PtrToStructure((IntPtr)(prim_group_ptr.ToInt64() + t * sizeof(PrimitiveGroup)), typeof(PrimitiveGroup));
                                    }
                                    Marshal.FreeHGlobal(ptr_to_primgroup);
                                    vertex_list.Clear();
                                    for (int t = 0; t < group_cnt; t++)
                                    {
                                        if (primGroup[t].type == PrimType.PT_STRIP)
                                        {
                                            for (int s = 0; s < primGroup[t].numIndices; s++)
                                            {
                                                vertex_list.Add(Marshal.ReadInt32(primGroup[t].indices,4*s));
                                            }
                                            vertex_list.Add(vertex_list[vertex_list.Count - 1]);
                                        }
                                        else if (primGroup[t].type == PrimType.PT_LIST)
                                        {
                                            List<Int32> temp_PT_List = new List<Int32>();
                                            for (int s = 0; s < primGroup[t].numIndices; s++)
                                            {
                                                temp_PT_List.Add(Marshal.ReadInt32(primGroup[t].indices, 4 * s));
                                            }
                                            for (int s = 0; s < (primGroup[t].numIndices / 3); s++)
                                            {
                                                if (vertex_list.Count % 2 != 0)
                                                {
                                                    vertex_list.Add(temp_PT_List[3 * s]);
                                                    vertex_list.Add(temp_PT_List[3 * s]);
                                                    vertex_list.Add(temp_PT_List[3 * s + 1]);
                                                    vertex_list.Add(temp_PT_List[3 * s + 2]);
                                                    vertex_list.Add(temp_PT_List[3 * s + 2]);
                                                }
                                                else
                                                {
                                                    vertex_list.Add(temp_PT_List[3 * s + 2]);
                                                    vertex_list.Add(temp_PT_List[3 * s + 2]);
                                                    vertex_list.Add(temp_PT_List[3 * s + 1]);
                                                    vertex_list.Add(temp_PT_List[3 * s]);
                                                    vertex_list.Add(temp_PT_List[3 * s]);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                System.Console.WriteLine(e.ToString());
                            }*/
                            //adding list to new_vert_field
                            int border_offset = 0;
                            mesh_vertex_borders_idx.Add(mesh_vertex_borders.Count);
                            if (mesh_vertex_borders.Count > 0)
                            {
                                border_offset = mesh_vertex_borders[mesh_vertex_borders.Count - 1];
                            }
                            border_offset += vertex_list.Count;
                            mesh_vertex_borders.Add(border_offset);
                            for (int t = 0; t < vertex_list.Count; t++)
                            {
                                new_vert_field.Add(new vertex_field(tso_scene.meshes[index_pos].vertices[vertex_list[t]]));
                            }
                        }
                        if (mesh_vertex_borders.Count > 1)
                        {
                            mesh_vertex_borders.RemoveAt(mesh_vertex_borders.Count - 1);
                            mesh_vertex_borders_idx.RemoveAt(mesh_vertex_borders_idx.Count - 1);
                        }

                    //}

                    //now it has correct vertex order
                    if (sub_meshes.Count > 0)
                    {
                        //there are sub meshes... creating them now
                        int vertex_acc_old = 0;
                        for (int j = 0; j <= mesh_vertex_borders.Count; j++)
                        {
                            vertex_field[] sub_field = null;
                            if (j == mesh_vertex_borders.Count)
                            {
                                sub_field = new vertex_field[new_vert_field.Count - vertex_acc_old];
                                for (int h = 0; h < sub_field.Length; h++)
                                {
                                    sub_field[h] = new vertex_field(new_vert_field[vertex_acc_old + h]);
                                }
                                //new_vert_field.CopyTo(vertex_acc_old, sub_field, 0, sub_field.Length);
                            }
                            else
                            {
                                sub_field = new vertex_field[mesh_vertex_borders[j] - vertex_acc_old];
                                for (int h = 0; h < sub_field.Length; h++)
                                {
                                    sub_field[h] = new vertex_field(new_vert_field[vertex_acc_old + h]);
                                }
                                //new_vert_field.CopyTo(vertex_acc_old, sub_field, 0, sub_field.Length);
                            }
                            if (j == 0)
                            {
                                tso_scene.meshes[index_pos].vertices = sub_field;
                                tso_scene.meshes[index_pos].vertex_count = (UInt32)sub_field.Length;
                            }
                            else
                            {
                                //an actual submesh
                                mesh new_submesh = new mesh();
                                new_submesh.sub_mesh_count = 0;
                                new_submesh.unknown1 = tso_scene.meshes[index_pos].unknown1;
                                new_submesh.vertex_count = (UInt32)sub_field.Length;
                                new_submesh.vertices = sub_field;
                                new_submesh.transform_matrix = null;
                                //now getting the name and the unknown 3 value

                                sArray = sub_meshes[j].Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                                sArray = sArray[0].Split(new string[] { "_sep_" }, StringSplitOptions.RemoveEmptyEntries);
                                string sub_name = sArray[0].Replace("_colon_", ":");
                                if (sub_name.Contains("_sub_") && sub_name.Substring(name.Length - 2, 2).Equals("_0"))
                                {
                                    sub_name = sub_name.Substring(0, sub_name.Length - 2);
                                }
                                new_submesh.name = sub_name.Substring(6);
                                new_submesh.unknown3 = System.UInt32.Parse(sArray[4]);
                                //get minimal bone_index_LUT based on new vertex field
                                List<UInt32> used_bone_indices = new List<UInt32>();
                                for (int h = 0; h < new_submesh.vertex_count; h++)
                                {
                                    for (int g = 0; g < new_submesh.vertices[h].bone_weight_entry_count; g++)
                                    {
                                        if (!used_bone_indices.Contains(new_submesh.vertices[h].bone_weight_field[g].bone_index))
                                        {
                                            used_bone_indices.Add(new_submesh.vertices[h].bone_weight_field[g].bone_index);
                                            new_submesh.vertices[h].bone_weight_field[g].bone_index = (UInt32)used_bone_indices.Count - 1;
                                        }
                                        else
                                        {
                                            new_submesh.vertices[h].bone_weight_field[g].bone_index = (UInt32)used_bone_indices.IndexOf(new_submesh.vertices[h].bone_weight_field[g].bone_index);
                                        }
                                    }
                                }
                                new_submesh.bone_index_LUT_entry_count = (UInt32)used_bone_indices.Count;
                                UInt32[] new_bone_index_LUT = new UInt32[used_bone_indices.Count];
                                for (int h = 0; h < used_bone_indices.Count; h++)
                                {
                                    new_bone_index_LUT[h] = tso_scene.meshes[index_pos].bone_index_LUT[used_bone_indices[h]];
                                }
                                new_submesh.bone_index_LUT = new_bone_index_LUT;
                                tso_scene.meshes.Add(new_submesh);
                            }

                            if (j < mesh_vertex_borders.Count)
                            {
                                vertex_acc_old = mesh_vertex_borders[j];
                            }
                        }
                        //get minimal bone_index_LUT based on new vertex field
                        List<UInt32> used_bone_indices_parent = new List<UInt32>();
                        for (int h = 0; h < tso_scene.meshes[index_pos].vertex_count; h++)
                        {
                            for (int g = 0; g < tso_scene.meshes[index_pos].vertices[h].bone_weight_entry_count; g++)
                            {
                                if (!used_bone_indices_parent.Contains(tso_scene.meshes[index_pos].vertices[h].bone_weight_field[g].bone_index))
                                {
                                    used_bone_indices_parent.Add(tso_scene.meshes[index_pos].vertices[h].bone_weight_field[g].bone_index);
                                    tso_scene.meshes[index_pos].vertices[h].bone_weight_field[g].bone_index = (UInt32)used_bone_indices_parent.Count - 1;
                                }
                                else
                                {
                                    tso_scene.meshes[index_pos].vertices[h].bone_weight_field[g].bone_index = (UInt32)used_bone_indices_parent.IndexOf(tso_scene.meshes[index_pos].vertices[h].bone_weight_field[g].bone_index);
                                }
                            }
                        }
                        tso_scene.meshes[index_pos].bone_index_LUT_entry_count = (UInt32)used_bone_indices_parent.Count;
                        UInt32[] new_bone_index_LUT_parent = new UInt32[used_bone_indices_parent.Count];
                        for (int h = 0; h < used_bone_indices_parent.Count; h++)
                        {
                            new_bone_index_LUT_parent[h] = tso_scene.meshes[index_pos].bone_index_LUT[used_bone_indices_parent[h]];
                        }
                        tso_scene.meshes[index_pos].bone_index_LUT = new_bone_index_LUT_parent;
                    }
                    else
                    {
                        tso_scene.meshes[index_pos].vertices = new_vert_field.ToArray();
                        tso_scene.meshes[index_pos].vertex_count = (UInt32)new_vert_field.ToArray().Length;
                        /*string all_verts = "";
                        for (int i = 0; i < tso_scene.meshes[index_pos].vertex_count; i++)
                        {
                            all_verts += tso_scene.meshes[index_pos].vertices[i].position[0] + "," +
                                         tso_scene.meshes[index_pos].vertices[i].position[1] + "," +
                                         tso_scene.meshes[index_pos].vertices[i].position[2] + ";\n";
                        }*/
                    }
                }
                else if (line.Contains("Frame "))
                {
                    ParseFrame(text_reader, frame, ref skelletons, ref line, texturePaths, ref tso_scene, filename);
                }
            }
        }

        public double vertex_normal_vs_face_normal_angle(float[] a, float[] b, float[] c, float[] an, float[] bn, float[] cn,ref double[] face_normal, ref double[] average_vn)
        {
            face_normal = calculate_normal_of_triangle_face(new double[] { (double)a[0], (double)a[1], (double)a[2] },
                                                                     new double[] { (double)b[0], (double)b[1], (double)b[2] },
                                                                     new double[] { (double)c[0], (double)c[1], (double)c[2] });
            //calculating average vertex normal

            average_vn = new double[3];
            //testing for sanity
            if ((System.Math.Abs(an[0] + bn[0]) < System.Math.Abs(cn[0])) ||
                (System.Math.Abs(cn[0] + bn[0]) < System.Math.Abs(an[0])) ||
                (System.Math.Abs(an[0] + cn[0]) < System.Math.Abs(bn[0])))
            {
                //wrong of first magnitude
                if ((System.Math.Abs(an[1] + bn[1]) < System.Math.Abs(cn[1])) ||
                   (System.Math.Abs(cn[1] + bn[1]) < System.Math.Abs(an[1])) ||
                   (System.Math.Abs(an[1] + cn[1]) < System.Math.Abs(bn[1])))
                {
                    //wrong of second magnitude
                    if ((System.Math.Abs(an[2] + bn[2]) < System.Math.Abs(cn[2])) ||
                      (System.Math.Abs(cn[2] + bn[2]) < System.Math.Abs(an[2])) ||
                      (System.Math.Abs(an[2] + cn[2]) < System.Math.Abs(bn[2])))
                    {
                        //appears wrong
                        //find the strange one
                        float[] dst = get_vertex_distances_triangle(an, bn, cn);
                        if ( (dst[1] > dst[0]) && (dst[2] > dst[0]))
                        {
                            //cn
                            average_vn[0] = (an[0] + bn[0]);
                            average_vn[1] = (an[1] + bn[1]);
                            average_vn[2] = (an[2] + bn[2]);
                        }
                        else if ( (dst[0] > dst[2]) && (dst[1] > dst[2]) )
                        {
                            //an
                            average_vn[0] = (bn[0] + cn[0]);
                            average_vn[1] = (bn[1] + cn[1]);
                            average_vn[2] = (bn[2] + cn[2]);
                        }
                        else if ((dst[0] > dst[1]) && (dst[2] > dst[1]))
                        {
                            //bn
                            average_vn[0] = (an[0] + cn[0]);
                            average_vn[1] = (an[1] + cn[1]);
                            average_vn[2] = (an[2] + cn[2]);
                        }
                        else
                        {
                            average_vn[0] = (an[0] + bn[0] + cn[0]);
                            average_vn[1] = (an[1] + bn[1] + cn[1]);
                            average_vn[2] = (an[2] + bn[2] + cn[2]);
                        }
                    }
                    else
                    {
                        average_vn[0] = (an[0] + bn[0] + cn[0]);
                        average_vn[1] = (an[1] + bn[1] + cn[1]);
                        average_vn[2] = (an[2] + bn[2] + cn[2]);
                    }
                }
                else
                {
                    average_vn[0] = (an[0] + bn[0] + cn[0]);
                    average_vn[1] = (an[1] + bn[1] + cn[1]);
                    average_vn[2] = (an[2] + bn[2] + cn[2]);
                }
            }
            else
            {
                average_vn[0] = (an[0] + bn[0] + cn[0]);
                average_vn[1] = (an[1] + bn[1] + cn[1]);
                average_vn[2] = (an[2] + bn[2] + cn[2]);
            }
            double dn = 1.0f / System.Math.Sqrt(average_vn[0] * average_vn[0] + average_vn[1] * average_vn[1] + average_vn[2] * average_vn[2]);
            average_vn[0] *= dn;
            average_vn[1] *= dn;
            average_vn[2] *= dn;

            //calculating angle between the two vectors
            //since the vectors are normalized already, this will be very easy.
            double angle = System.Math.Acos(average_vn[0] * face_normal[0] + average_vn[1] * face_normal[1] + average_vn[2] * face_normal[2]);
            return angle;
        }

        public bone_node[] get_path_traversal_bone_nodes(bone_node[] skelleton, bone_node target)
        {
            bone_node[] ret_bones = null;
            for (int i = 0; i < skelleton.Length; i++)
            {
                if (skelleton[i].name.CompareTo(target.name) == 0)
                {
                    //found the target;
                    ret_bones = new bone_node[1];
                    ret_bones[0] = skelleton[i];
                    break;
                }
                else if (skelleton[i].child_nodes != null)
                {
                    ret_bones = get_path_traversal_bone_nodes(skelleton[i].child_nodes, target);
                    if (ret_bones != null)
                    {
                        bone_node[] t_ret_bones = new bone_node[ret_bones.Length + 1];
                        for (int j = 0; j < ret_bones.Length; j++)
                        {
                            t_ret_bones[j] = ret_bones[j];
                        }
                        t_ret_bones[ret_bones.Length] = skelleton[i];
                        ret_bones = t_ret_bones;
                        break;
                    }
                }
            }
            return ret_bones;
        }

        public static List<int> find_neighbours_strip(ref vertex_field[] mesh_verts, ref List<int[]> faces, ref List<int> un_booked_faces, ref int lower_bound, ref int upper_bound, int start)
        {
            List<int> ret = new List<int>();
            List<int> local_booked = new List<int>();
            ret.Add(start);
            local_booked.Add(start);
            int f = start;
            //now make the strip
            while ((f >= 0))// && (ret.Count < 2))
            {
                for (int i = lower_bound; i < upper_bound; i++)
                {
                    if ((un_booked_faces.BinarySearch(i) >= 0) && !local_booked.Contains(i))
                    {
                        if (ret.Count % 2 == 0)
                        {
                            if ((faces[f][0] == faces[i][1]) && (faces[f][1] == faces[i][0]) && (faces[f][2] != faces[i][2]))
                            {
                                ret.Add(i);
                                local_booked.Add(i);
                                f = i;
                                break;
                            }
                        }
                        else
                        {
                            if ((faces[f][2] == faces[i][1]) && (faces[f][1] == faces[i][2]) && (faces[f][0] != faces[i][0]))
                            {
                                ret.Add(i);
                                local_booked.Add(i);
                                f = i;
                                break;
                            }
                        }
                        /*if ((ret.Count % 2 == 0))
                        {
                            /*if ((faces[f][0] == faces[i][0]) && (faces[f][1] == faces[i][2]) && (faces[f][2] != faces[i][1]))
                            {
                                ret.Add(i);
                                local_booked.Add(i);
                                f = i;
                                //right shift
                                int t1 = faces[i][2];
                                faces[i][2] = faces[i][1];
                                faces[i][1] = faces[i][0];
                                faces[i][0] = t1;
                                break;
                            }
                            else*/
                            /*if ((faces[f][0] == faces[i][1]) && (faces[f][1] == faces[i][0]) && (faces[f][2] != faces[i][2]))
                            {
                                ret.Add(i);
                                local_booked.Add(i);
                                f = i;
                                break;
                            }
                            /*else if ((faces[f][0] == faces[i][2]) && (faces[f][1] == faces[i][1]) && (faces[f][2] != faces[i][0]))
                            {
                                ret.Add(i);
                                local_booked.Add(i);
                                f = i;
                                //left shift
                                int t1 = faces[i][0];
                                faces[i][0] = faces[i][1];
                                faces[i][1] = faces[i][2];
                                faces[i][2] = t1;
                                break;
                            }*/
                        /*}
                        else
                        {
                            /*if ((faces[f][2] == faces[i][0]) && (faces[f][1] == faces[i][1]) && (faces[f][0] != faces[i][2]))
                            {
                                ret.Add(i);
                                local_booked.Add(i);
                                f = i;
                                //right shift
                                int t1 = faces[i][2];
                                faces[i][2] = faces[i][1];
                                faces[i][1] = faces[i][0];
                                faces[i][0] = t1;
                                break;
                            }
                            else*/
                            /*if ((faces[f][2] == faces[i][1]) && (faces[f][1] == faces[i][2]) && (faces[f][0] != faces[i][0]))
                            {
                                ret.Add(i);
                                local_booked.Add(i);
                                f = i;
                                break;
                            }
                            /*else if ((faces[f][2] == faces[i][2]) && (faces[f][1] == faces[i][0]) && (faces[f][0] != faces[i][1]))
                            {
                                ret.Add(i);
                                local_booked.Add(i);
                                f = i;
                                //left shift
                                int t1 = faces[i][0];
                                faces[i][0] = faces[i][1];
                                faces[i][1] = faces[i][2];
                                faces[i][2] = t1;
                                break;
                            }
                        }*/
                    }
                    if (i == upper_bound - 1)
                    {
                        f = -1;
                    }
                }
            }
            return ret;
        }

        public static double[] calculate_normal_of_triangle_face(double[] a, double[] b, double[] c)
        {
            double[] ret = new double[3];
            double[] p = new double[3];
            double[] q = new double[3];
            double[] n = new double[3];

            p[0] = b[0] - a[0];
            p[1] = b[1] - a[1];
            p[2] = b[2] - a[2];

            q[0] = c[0] - a[0];
            q[1] = c[1] - a[1];
            q[2] = c[2] - a[2];

            n[0] = p[1] * q[2] - p[2] * q[1];
            n[1] = p[2] * q[0] - p[0] * q[2];
            n[2] = p[0] * q[1] - p[1] * q[0];

            double d = 1.0/System.Math.Sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2]);

            ret[0] = d * n[0];
            ret[1] = d * n[1];
            ret[2] = d * n[2];
            return ret;
        }

        public void write_scene_to_file(string file_name, ref scene tso_scene)
        {
            if (System.IO.File.Exists(file_name))
            {
                int cnt = 1;
                string new_file_name = file_name;
                while (System.IO.File.Exists(new_file_name))
                {
                    new_file_name = file_name.Substring(0, file_name.Length - 3) + cnt.ToString() + ".tso";
                    cnt++;
                }
                System.IO.File.Move(file_name, new_file_name);
            }
            System.IO.BinaryWriter writer;
            try
            {
                writer = new System.IO.BinaryWriter(System.IO.File.Create(file_name));
            }
            catch (Exception)
            {
                System.Threading.Thread.Sleep(1000);
                try
                {
                    writer = new System.IO.BinaryWriter(System.IO.File.OpenWrite(file_name));
                }
                catch (Exception e)
                {
                    System.Console.Out.WriteLine(e.ToString());
                    return;
                }
            }

            //first the header must be written
            byte[] magic_header = System.Text.Encoding.ASCII.GetBytes("TSO1");
            writer.Write(magic_header, 0, 4);
            writer.Write(System.BitConverter.GetBytes((UInt32)(tso_scene.bone_node_count)));
            //now the skeleton bone_nodes
            writer.Write(tso_scene.tso_data());
            //now the textures
            writer.Write(System.BitConverter.GetBytes((UInt32)(tso_scene.textures.Length)));
            for (int i = 0; i < tso_scene.textures.Length; i++)
            {
                writer.Write(tso_scene.textures[i].data_stream);
            }
            //now the shader files
            writer.Write(System.BitConverter.GetBytes((UInt32)(tso_scene.scripts.Length)));
            for (int i = 0; i < tso_scene.scripts.Length; i++)
            {
                string[] parse_rel_path = tso_scene.scripts[i].file_name.Split(new string[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);
                string script_file_name = parse_rel_path[0];
                byte[] file_name_byte = System.Text.Encoding.ASCII.GetBytes(script_file_name);
                writer.Write(file_name_byte);
                writer.Write(new byte[] { 0x00 });
                writer.Write(System.BitConverter.GetBytes((UInt32)(tso_scene.scripts[i].script_data.Length)));
                for (int j = 0; j < tso_scene.scripts[i].script_data.Length; j++)
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes(tso_scene.scripts[i].script_data[j]));
                    writer.Write(new byte[] { 0x00 });
                }
                if (tso_scene.scripts[i].sub_scripts != null)
                {
                    writer.Write(System.BitConverter.GetBytes((UInt32)(tso_scene.scripts[i].sub_scripts.Length)));
                    for (int h = 0; h < tso_scene.scripts[i].sub_scripts.Length; h++)
                    {
                        parse_rel_path = tso_scene.scripts[i].sub_scripts[h].file_name.Split(new string[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);
                        string script_file_path = parse_rel_path[0];
                        script_file_name = parse_rel_path[1];
                        byte[] sub_file_path_byte = System.Text.Encoding.ASCII.GetBytes(script_file_path);
                        byte[] sub_file_name_byte = System.Text.Encoding.ASCII.GetBytes(script_file_name);
                        writer.Write(sub_file_path_byte);
                        writer.Write(new byte[] { 0x00 });
                        writer.Write(sub_file_name_byte);
                        writer.Write(new byte[] { 0x00 });
                        writer.Write(System.BitConverter.GetBytes((UInt32)(tso_scene.scripts[i].sub_scripts[h].script_data.Length)));
                        for (int k = 0; k < tso_scene.scripts[i].sub_scripts[h].script_data.Length; k++)
                        {
                            writer.Write(System.Text.Encoding.ASCII.GetBytes(tso_scene.scripts[i].sub_scripts[h].script_data[k]));
                            writer.Write(new byte[] { 0x00 });
                        }
                    }
                }
            }
            //now the mesh data
            write_tso_scene_mesh_data(ref tso_scene, ref writer);
            writer.Close();
        }

        public void write_tso_scene_mesh_data(ref scene tso_scene, ref System.IO.BinaryWriter writer)
        {
            if (use_mesh_binary)
            {
                System.IO.BinaryReader reader = new System.IO.BinaryReader(System.IO.File.OpenRead(tso_scene.file_name_bin_mesh));
                byte[] tso_bin_mesh = reader.ReadBytes((int)reader.BaseStream.Length);
                writer.Write(tso_bin_mesh);
                reader.Close();
            }
            else
            {
                //first get the mesh count without counting sub_meshes
                int base_mesh_cnt = 0;
                mesh[] meshes = tso_scene.meshes.ToArray();
                for (int i = 0; i < meshes.Length; i++)
                {
                    if (meshes[i].sub_mesh_count != 0)
                    {
                        base_mesh_cnt++;
                    }
                }
                //this number can now be written as header
                writer.Write(System.BitConverter.GetBytes((UInt32)(base_mesh_cnt)));
                for (int i = 0; i < meshes.Length; i++)
                {
                    if (meshes[i].sub_mesh_count != 0)
                    {
                        //okay this must be a base_mesh...
                        //lets write
                        writer.Write(System.Text.Encoding.ASCII.GetBytes(meshes[i].name));
                        writer.Write(new byte[] { 0x00 });
                        for (int j = 0; j < 16; j++)
                        {
                            writer.Write(System.BitConverter.GetBytes((Single)(meshes[i].transform_matrix[j])));
                        }
                        writer.Write(System.BitConverter.GetBytes((UInt32)(meshes[i].unknown1)));
                        writer.Write(System.BitConverter.GetBytes((UInt32)(meshes[i].sub_mesh_count)));
                        writer.Write(System.BitConverter.GetBytes((UInt32)(meshes[i].unknown3)));
                        writer.Write(System.BitConverter.GetBytes((UInt32)(meshes[i].bone_index_LUT_entry_count)));
                        for (int a = 0; a < meshes[i].bone_index_LUT_entry_count; a++)
                        {
                            writer.Write(System.BitConverter.GetBytes((UInt32)(meshes[i].bone_index_LUT[a])));
                        }
                        writer.Write(System.BitConverter.GetBytes((UInt32)(meshes[i].vertex_count)));
                        for (int a = 0; a < meshes[i].vertex_count; a++)
                        {
                            //position
                            writer.Write(System.BitConverter.GetBytes((Single)(meshes[i].vertices[a].position[0])));
                            writer.Write(System.BitConverter.GetBytes((Single)(meshes[i].vertices[a].position[1])));
                            writer.Write(System.BitConverter.GetBytes((Single)(meshes[i].vertices[a].position[2])));
                            //normal
                            writer.Write(System.BitConverter.GetBytes((Single)(meshes[i].vertices[a].normal[0])));
                            writer.Write(System.BitConverter.GetBytes((Single)(meshes[i].vertices[a].normal[1])));
                            writer.Write(System.BitConverter.GetBytes((Single)(meshes[i].vertices[a].normal[2])));
                            //UV
                            writer.Write(System.BitConverter.GetBytes((Single)(meshes[i].vertices[a].UV[0])));
                            writer.Write(System.BitConverter.GetBytes((Single)(meshes[i].vertices[a].UV[1])));
                            //bone weights
                            writer.Write(System.BitConverter.GetBytes((UInt32)(meshes[i].vertices[a].bone_weight_entry_count)));
                            UInt32[] bone_weight_indexes = new UInt32[meshes[i].vertices[a].bone_weight_entry_count];
                            Single[] bone_weight_values = new Single[meshes[i].vertices[a].bone_weight_entry_count];
                            for (int b = 0; b < meshes[i].vertices[a].bone_weight_entry_count; b++)
                            {
                                bone_weight_indexes[b] = meshes[i].vertices[a].bone_weight_field[b].bone_index;
                                bone_weight_values[b] = meshes[i].vertices[a].bone_weight_field[b].vertex_bone_weight;
                            }
                            Array.Sort(bone_weight_indexes, bone_weight_values);
                            for (int b = 0; b < meshes[i].vertices[a].bone_weight_entry_count; b++)
                            {
                                //writer.Write(System.BitConverter.GetBytes((UInt32)(meshes[i].vertices[a].bone_weight_field[b].bone_index)));
                                //writer.Write(System.BitConverter.GetBytes((Single)(meshes[i].vertices[a].bone_weight_field[b].vertex_bone_weight)));
                                writer.Write(System.BitConverter.GetBytes((UInt32)bone_weight_indexes[b]));
                                writer.Write(System.BitConverter.GetBytes((Single)bone_weight_values[b]));
                            }
                        }
                        if (meshes[i].sub_mesh_count > 1)
                        {
                            //there are sub_meshes
                            int true_cnt = (int)meshes[i].sub_mesh_count - 1;
                            for (int l = 0; l < true_cnt; l++)
                            {
                                //first we create the supposed sub mesh name
                                string supposed_name = meshes[i].name + "_sub_" + (l + 1).ToString();
                                //now find a mesh in the mesh field with that name
                                int pos = 0;
                                while ((!meshes[pos].name.Equals(supposed_name)) && (pos < meshes.Length))
                                {
                                    pos++;
                                }
                                //okay now we have the right mesh for this
                                //lets write it
                                writer.Write(System.BitConverter.GetBytes((UInt32)(meshes[pos].unknown3)));
                                writer.Write(System.BitConverter.GetBytes((UInt32)(meshes[pos].bone_index_LUT_entry_count)));
                                for (int a = 0; a < meshes[pos].bone_index_LUT_entry_count; a++)
                                {
                                    writer.Write(System.BitConverter.GetBytes((UInt32)(meshes[pos].bone_index_LUT[a])));
                                }
                                writer.Write(System.BitConverter.GetBytes((UInt32)(meshes[pos].vertex_count)));
                                for (int a = 0; a < meshes[pos].vertex_count; a++)
                                {
                                    //position
                                    writer.Write(System.BitConverter.GetBytes((Single)(meshes[pos].vertices[a].position[0])));
                                    writer.Write(System.BitConverter.GetBytes((Single)(meshes[pos].vertices[a].position[1])));
                                    writer.Write(System.BitConverter.GetBytes((Single)(meshes[pos].vertices[a].position[2])));
                                    //normal
                                    writer.Write(System.BitConverter.GetBytes((Single)(meshes[pos].vertices[a].normal[0])));
                                    writer.Write(System.BitConverter.GetBytes((Single)(meshes[pos].vertices[a].normal[1])));
                                    writer.Write(System.BitConverter.GetBytes((Single)(meshes[pos].vertices[a].normal[2])));
                                    //UV
                                    writer.Write(System.BitConverter.GetBytes((Single)(meshes[pos].vertices[a].UV[0])));
                                    writer.Write(System.BitConverter.GetBytes((Single)(meshes[pos].vertices[a].UV[1])));
                                    //bone weights
                                    writer.Write(System.BitConverter.GetBytes((UInt32)(meshes[pos].vertices[a].bone_weight_entry_count)));
                                    for (int b = 0; b < meshes[pos].vertices[a].bone_weight_entry_count; b++)
                                    {
                                        writer.Write(System.BitConverter.GetBytes((UInt32)(meshes[pos].vertices[a].bone_weight_field[b].bone_index)));
                                        writer.Write(System.BitConverter.GetBytes((Single)(meshes[pos].vertices[a].bone_weight_field[b].vertex_bone_weight)));
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public void parseMaterials(System.IO.StreamReader text_reader, ref string line, ref System.Collections.ArrayList texturePaths, ref scene tso_scene)
        {
            if (tso_scene.mat_list == null)
            {
                tso_scene.mat_list = new List<material>();
            }
            string[] sArray = null;
            if (line.Contains("Material "))
            {
                material new_material = new material();
                new_material.name = line.Substring(line.IndexOf("Material ") + 9).Replace(" ", "").Replace("{", "");
                tso_scene.mat_list.Add(new_material);

                //the other stuff is not important... but maybe it is used somewhen later on...
                sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                float[] ambient = new float[] { Single.Parse(sArray[0], Culture), Single.Parse(sArray[1], Culture), Single.Parse(sArray[2], Culture), Single.Parse(sArray[3], Culture) };
                line = text_reader.ReadLine();
                sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                float specularPower = Single.Parse(sArray[0], Culture);
                line = text_reader.ReadLine();
                sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                float[] specular = new float[] { Single.Parse(sArray[0], Culture), Single.Parse(sArray[1], Culture), Single.Parse(sArray[2], Culture), 1f };
                line = text_reader.ReadLine();
                sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                float[] emissive = new float[] { Single.Parse(sArray[0], Culture), Single.Parse(sArray[1], Culture), Single.Parse(sArray[2], Culture), 1f };
                List<string> materialTextures = new List<string>();

                while (!(line = text_reader.ReadLine()).Contains("}"))
                {
                    if (line.Contains("TextureFilename"))
                    {
                        while ((line = text_reader.ReadLine()).Split(spaceDelimiters, StringSplitOptions.RemoveEmptyEntries).Length <= 0) ;
                        sArray = line.Split(entryDelimiters, StringSplitOptions.RemoveEmptyEntries);
                        if (!texturePaths.Contains(sArray[0]))
                        {
                            texturePaths.Add(sArray[0]);
                        }

                        if (line.Contains("{"))
                        {
                            ParseSkipSection(text_reader, ref line);
                        }
                        SkipToEndOfSection(text_reader, ref line);
                    }
                    else
                    {
                        ParseSkipSection(text_reader, ref line);
                    }
                }

            }
        }

        public void process_X_File(string rel_file_path, string parent_dir, ref scene tso_scene)
        {
            System.IO.StreamReader text_reader = new System.IO.StreamReader(System.IO.File.OpenRead(parent_dir + rel_file_path), System.Text.Encoding.ASCII);
            //try
            //{

            string line;
            List<string> texturePaths = new List<string>();
            System.Collections.ArrayList texture_paths = new System.Collections.ArrayList();
            line = text_reader.ReadLine();
            while ((line = text_reader.ReadLine()) != null)
            {
                if (line.Contains("//!MOD_TSO"))
                {
                    //take the binary.
                    use_mesh_binary = true;
                }
                else if (line.Contains("template"))
                {
                    ParseSkipSection(text_reader, ref line);
                }
                else if (line.Contains("Frame "))
                {
                    ParseFrame(text_reader, new bone_node(), ref tso_scene.skellettons, ref line, texturePaths, ref tso_scene, rel_file_path);
                }
                else if (line.Contains("AnimationSet "))
                {
                    //no animation yet
                }
                else if (line.Contains("Animation "))
                {
                    //no animation yet
                }
                else if (line.Contains("Material "))
                {
                    //no materials... encoded into shaders
                }
                else
                {
                    ParseSkipSection(text_reader, ref line);
                }
            }

            /*}
            catch (Exception e)
            {
                throw e;
            }*/
            text_reader.Close();
        }

        public float[] get_vertex_distances_triangle(float[] a, float[] b, float[] c)
        {
            float[] ret = new float[3];
            ret[0] = (float)System.Math.Sqrt((double)((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]) + (a[2] - b[2]) * (a[2] - b[2])));
            ret[1] = (float)System.Math.Sqrt((double)((a[0] - c[0]) * (a[0] - c[0]) + (a[1] - c[1]) * (a[1] - c[1]) + (a[2] - c[2]) * (a[2] - c[2])));
            ret[2] = (float)System.Math.Sqrt((double)((c[0] - b[0]) * (c[0] - b[0]) + (c[1] - b[1]) * (c[1] - b[1]) + (c[2] - b[2]) * (c[2] - b[2])));
            return ret;
        }

        public void add_SubScript(string rel_file_path, string parent_dir, ref scene tso_scene)
        {
            System.IO.StreamReader text_reader = new System.IO.StreamReader(System.IO.File.OpenRead(parent_dir + rel_file_path), System.Text.Encoding.ASCII);
            script sub_script = new script();
            rel_file_path = "\\" + rel_file_path.Substring(8);
            sub_script.file_name = rel_file_path;
            //string[] parse_rel_path = rel_file_path.Split(new string[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);
            //string file_path = parse_rel_path[0];
            //string file_name = parse_rel_path[1];
            //byte[] file_path_byte = System.Text.Encoding.ASCII.GetBytes(file_path);
            //byte[] file_name_byte = System.Text.Encoding.ASCII.GetBytes(file_name);
            System.Collections.ArrayList read_in = new System.Collections.ArrayList();
            while (!text_reader.EndOfStream)
            {
                read_in.Add((string)text_reader.ReadLine());
            }
            sub_script.script_data = (string[])read_in.ToArray("".GetType());
            if (tso_scene.scripts[0].sub_scripts != null)
            {
                int current_subscript_cnt = tso_scene.scripts[0].sub_scripts.Length;
                script[] new_subscripts = new script[current_subscript_cnt + 1];
                for (int i = 0; i < current_subscript_cnt; i++)
                {
                    new_subscripts[i] = tso_scene.scripts[0].sub_scripts[i];
                }
                new_subscripts[current_subscript_cnt] = sub_script;
                tso_scene.scripts[0].sub_scripts = new_subscripts;
            }
            else
            {
                tso_scene.scripts[0].sub_scripts = new script[1];
                tso_scene.scripts[0].sub_scripts[0] = sub_script;
            }
            text_reader.Close();
        }

        public void add_BMP_texture(string rel_file_path, string parent_dir, ref scene tso_scene)
        {
            try
            {
                texture tex = new texture();
                reader = new System.IO.BinaryReader(System.IO.File.OpenRead(parent_dir + rel_file_path));
                byte[] bmp_header = reader.ReadBytes(54);
                UInt32 width = System.BitConverter.ToUInt32(bmp_header, 18);
                UInt32 height = System.BitConverter.ToUInt32(bmp_header, 22);
                UInt32 channels = (UInt32)(System.BitConverter.ToUInt16(bmp_header, 28) / 8);
                string[] parse_rel_path = rel_file_path.Split(new string[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);
                string file_path = parse_rel_path[0].Substring(7);
                string file_name = "\"" + parse_rel_path[1] + "\"";
                int data_length = 12 + file_name.Length + file_path.Length + 2 + (int)(width * height * channels); //the additional 2 is for the null seperators
                byte[] texture_data_stream = new byte[data_length];
                byte[] width_bytes = System.BitConverter.GetBytes(width);
                byte[] height_bytes = System.BitConverter.GetBytes(height);
                byte[] channel_bytes = System.BitConverter.GetBytes(channels);
                byte[] file_name_bytes = System.Text.Encoding.ASCII.GetBytes(file_name);
                byte[] file_path_bytes = System.Text.Encoding.ASCII.GetBytes(file_path);
                int z = 0;

                for (int i = 0; i < file_path_bytes.Length; i++)
                {
                    texture_data_stream[z] = file_path_bytes[i];
                    z++;
                }
                texture_data_stream[z] = 0x00;
                z++;
                for (int i = 0; i < file_name_bytes.Length; i++)
                {
                    texture_data_stream[z] = file_name_bytes[i];
                    z++;
                }
                texture_data_stream[z] = 0x00;
                z++;
                for (int i = 0; i < 12; i++)
                {
                    if (i < 4)
                    {
                        texture_data_stream[z] = width_bytes[i];
                        z++;
                    }
                    else if (i < 8)
                    {
                        texture_data_stream[z] = height_bytes[i - 4];
                        z++;
                    }
                    else if (i < 12)
                    {
                        texture_data_stream[z] = channel_bytes[i - 8];
                        z++;
                    }
                }
                byte[] pixel_data = reader.ReadBytes((int)(width * height * channels));
                //correct channel order
                for (int i = 3; i < pixel_data.Length; i += 4)
                {
                    byte temp = pixel_data[i - 3];
                    pixel_data[i - 3] = pixel_data[i - 1];
                    pixel_data[i - 1] = temp;
                }
                pixel_data.CopyTo(texture_data_stream, z);
                reader.Close();
                tex.data_stream = texture_data_stream;
                tex.file_name = file_name;
                tex.file_path = file_path;

                if (tso_scene.textures != null)
                {
                    int current_texture_cnt = tso_scene.textures.Length;
                    texture[] new_tex_set = new texture[current_texture_cnt + 1];
                    for (int i = 0; i < current_texture_cnt; i++)
                    {
                        new_tex_set[i] = tso_scene.textures[i];
                    }
                    new_tex_set[current_texture_cnt] = tex;
                    tso_scene.textures = new_tex_set;
                }
                else
                {
                    tso_scene.textures = new texture[1];
                    tso_scene.textures[0] = tex;
                }
            }
            catch (Exception e)
            {
                System.Console.Out.WriteLine("An expception occurd while reading in a bmp texture. Thus this TSO file will be incomplete.");
                System.Console.Out.WriteLine(e.ToString());
            }
        }

        public void add_TGA_texture(string rel_file_path, string parent_dir, ref scene tso_scene)
        {
            try
            {
                texture tex = new texture();
                reader = new System.IO.BinaryReader(System.IO.File.OpenRead(parent_dir + rel_file_path));
                byte[] tga_header = reader.ReadBytes(18);
                UInt32 width = System.BitConverter.ToUInt16(tga_header, 12);
                UInt32 height = System.BitConverter.ToUInt16(tga_header, 14);
                UInt32 channels = (UInt32)(tga_header[16] / 8);
                string[] parse_rel_path = rel_file_path.Split(new string[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);
                string file_path = parse_rel_path[0].Substring(7);
                string file_name = "\"" + parse_rel_path[1] + "\"";
                int data_length = 12 + file_name.Length + file_path.Length + 2 + (int)(width * height * channels); //the additional 2 is for the null seperators
                byte[] texture_data_stream = new byte[data_length];
                byte[] width_bytes = System.BitConverter.GetBytes(width);
                byte[] height_bytes = System.BitConverter.GetBytes(height);
                byte[] channel_bytes = System.BitConverter.GetBytes(channels);
                byte[] file_name_bytes = System.Text.Encoding.ASCII.GetBytes(file_name);
                byte[] file_path_bytes = System.Text.Encoding.ASCII.GetBytes(file_path);
                int z = 0;

                for (int i = 0; i < file_path_bytes.Length; i++)
                {
                    texture_data_stream[z] = file_path_bytes[i];
                    z++;
                }
                texture_data_stream[z] = 0x00;
                z++;
                for (int i = 0; i < file_name_bytes.Length; i++)
                {
                    texture_data_stream[z] = file_name_bytes[i];
                    z++;
                }
                texture_data_stream[z] = 0x00;
                z++;
                for (int i = 0; i < 12; i++)
                {
                    if (i < 4)
                    {
                        texture_data_stream[z] = width_bytes[i];
                        z++;
                    }
                    else if (i < 8)
                    {
                        texture_data_stream[z] = height_bytes[i - 4];
                        z++;
                    }
                    else if (i < 12)
                    {
                        texture_data_stream[z] = channel_bytes[i - 8];
                        z++;
                    }
                }
                byte[] pixel_data = reader.ReadBytes((int)(width * height * channels));
                for (int i = 3; i < pixel_data.Length; i += 4)
                {
                    byte temp = pixel_data[i - 3];
                    pixel_data[i - 3] = pixel_data[i - 1];
                    pixel_data[i - 1] = temp;
                }
                pixel_data.CopyTo(texture_data_stream, z);
                reader.Close();
                tex.data_stream = texture_data_stream;
                tex.file_name = file_name;
                tex.file_path = file_path;

                if (tso_scene.textures != null)
                {
                    int current_texture_cnt = tso_scene.textures.Length;
                    texture[] new_tex_set = new texture[current_texture_cnt + 1];
                    for (int i = 0; i < current_texture_cnt; i++)
                    {
                        new_tex_set[i] = tso_scene.textures[i];
                    }
                    new_tex_set[current_texture_cnt] = tex;
                    tso_scene.textures = new_tex_set;
                }
                else
                {
                    tso_scene.textures = new texture[1];
                    tso_scene.textures[0] = tex;
                }
            }
            catch (Exception e)
            {
                System.Console.Out.WriteLine("An expception occurd while reading in a bmp texture. Thus this TSO file will be incomplete.");
                System.Console.Out.WriteLine(e.ToString());
            }
        }

        public string[] get_all_files_from_source_path(string source_path)
        {
            string[] ret = null;

            ret = System.IO.Directory.GetFiles(source_path, "*", System.IO.SearchOption.AllDirectories);
            for (int i = 0; i < ret.Length; i++)
            {
                //only relative pathes from the source path
                ret[i] = ret[i].Replace(source_path, "");
            }

            return ret;
        }
    }

    class Decrypter
    {
        System.IO.BinaryReader reader;
        private Boolean parsed_meshes = false;

        public byte[] mesh_binary_data;

        public struct scene
        {
            public bone_node[] skellettons; //each entry represents a root bone_node of a whole skelleton
            public texture[] textures;
            public script[] scripts;
            public mesh[] meshes;
            public UInt32 bone_node_count;

            public bool is_vertex_triple_disjunct(vertex_field v1, vertex_field v2, vertex_field v3)
            {
                bool is_disjunct = true;
                if ((v1.position[0] == v2.position[0]) && (v1.position[1] == v2.position[1]) && (v1.position[2] == v2.position[2]))
                {
                    is_disjunct = false;
                }
                if ((v1.position[0] == v3.position[0]) && (v1.position[1] == v3.position[1]) && (v1.position[2] == v3.position[2]))
                {
                    is_disjunct = false;
                }
                if ((v2.position[0] == v3.position[0]) && (v2.position[1] == v3.position[1]) && (v2.position[2] == v3.position[2]))
                {
                    is_disjunct = false;
                }
                return is_disjunct;
            }

            public void flatten_skelleton(bone_node[] skelleton, ref UInt32 offset, ref bone_node[] skelleton_flat)
            {
                for (int i = 0; i < skelleton.Length; i++)
                {
                    skelleton_flat[offset] = skelleton[i];
                    offset++;
                    if (skelleton[i].child_nodes != null)
                    {
                        flatten_skelleton(skelleton[i].child_nodes, ref offset, ref skelleton_flat);
                    }
                }
            }

            //might not work if there this triangle is coplanar to the z-plane
            public bool is_triangle_clockwise(Single[][] triangle)
            {
                int n = 3;                      /* Number of vertices */
                Single area;
                int i;

                area = triangle[n - 1][0] * triangle[0][1] - triangle[0][0] * triangle[n - 1][1];

                for (i = 0; i < n - 1; i++)
                {
                    area += triangle[i][0] * triangle[i + 1][1] - triangle[i + 1][0] * triangle[i][1];
                }

                bool CW = false;
                if (area >= 0.0)
                {
                    CW = false;
                }
                else
                {
                    CW = true;
                }
                return CW;
            }

            public Single[][] get_bone_transform_matrix_from_skelleton(bone_node[] skelleton, bone_node target)
            {
                Single[][] ret_matrix = new Single[][] { new Single[4], new Single[4], new Single[4], new Single[4] };
                //first we have to find the traversal path to the destination bone_node
                //thats important for calculating the combined transform matrix of all
                //bone_nodes on the path
                bone_node[] path_traversal_bones = get_path_traversal_bone_nodes(skelleton, target);
                Single[] multiply_matrix = new Single[16];
                multiply_matrix.Initialize();
                //must be idetntity matrix
                multiply_matrix[0] = 1.0f;
                multiply_matrix[5] = 1.0f;
                multiply_matrix[10] = 1.0f;
                multiply_matrix[15] = 1.0f;
                for (int i = 0; i < path_traversal_bones.Length; i++)
                {
                    multiply_matrix = MatrixMultiply44(multiply_matrix, path_traversal_bones[i].transformation_matrix);
                }
                //now inverse it...
                ret_matrix = MatrixInverse44(new Single[][] {new Single[] {multiply_matrix[0], multiply_matrix[1], multiply_matrix[2], multiply_matrix[3]},
                                                                                         new Single[] {multiply_matrix[4], multiply_matrix[5], multiply_matrix[6], multiply_matrix[7]},
                                                                                         new Single[] {multiply_matrix[8], multiply_matrix[9], multiply_matrix[10], multiply_matrix[11]},
                                                                                         new Single[] {multiply_matrix[12], multiply_matrix[13], multiply_matrix[14], multiply_matrix[15]}});
                return ret_matrix;
            }

            public bone_node[] get_path_traversal_bone_nodes(bone_node[] skelleton, bone_node target)
            {
                bone_node[] ret_bones = null;
                for (int i = 0; i < skelleton.Length; i++)
                {
                    if (skelleton[i].name.CompareTo(target.name) == 0)
                    {
                        //found the target;
                        ret_bones = new bone_node[1];
                        ret_bones[0] = skelleton[i];
                        break;
                    }
                    else if (skelleton[i].child_nodes != null)
                    {
                        ret_bones = get_path_traversal_bone_nodes(skelleton[i].child_nodes, target);
                        if (ret_bones != null)
                        {
                            bone_node[] t_ret_bones = new bone_node[ret_bones.Length + 1];
                            for (int j = 0; j < ret_bones.Length; j++)
                            {
                                t_ret_bones[j] = ret_bones[j];
                            }
                            t_ret_bones[ret_bones.Length] = skelleton[i];
                            ret_bones = t_ret_bones;
                            break;
                        }
                    }
                }
                return ret_bones;
            }

            public static float[] MatrixMultiply44(float[] m1, float[] m2)
            {
                float[] dest = new float[16];
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        dest[i * 4 + j] = (float)(((double)m1[4 * i] * m2[j]) + (m1[4 * i + 1] * m2[4 + j]) + (m1[4 * i + 2] * m2[8 + j]) + (m1[4 * i + 3] * m2[12 + j]));
                    }
                }
                return dest;
            }

            //Alamar's matrix inverse calculator method. Thx to Alamar.
            public static float[][] MatrixInverse44(float[][] matrix)
            {
                float[][] inverse = new float[4][];
                for (int i = 0; i < 4; i++)
                {
                    inverse[i] = new float[4];
                }

                double m22_1 = (matrix[2][2] * matrix[3][3]) - (matrix[3][2] * matrix[2][3]);
                double m22_2 = (matrix[2][1] * matrix[3][3]) - (matrix[3][1] * matrix[2][3]);
                double m22_3 = (matrix[2][1] * matrix[3][2]) - (matrix[3][1] * matrix[2][2]);
                double m22_4 = (matrix[2][0] * matrix[3][3]) - (matrix[3][0] * matrix[2][3]);
                double m22_5 = (matrix[2][0] * matrix[3][2]) - (matrix[3][0] * matrix[2][2]);
                double m22_6 = (matrix[2][0] * matrix[3][1]) - (matrix[3][0] * matrix[2][1]);
                double m22_7 = (matrix[1][2] * matrix[3][3]) - (matrix[3][2] * matrix[1][3]);
                double m22_8 = (matrix[1][1] * matrix[3][3]) - (matrix[3][1] * matrix[1][3]);
                double m22_9 = (matrix[1][1] * matrix[3][2]) - (matrix[3][1] * matrix[1][2]);
                double m22_10 = (matrix[1][0] * matrix[3][3]) - (matrix[3][0] * matrix[1][3]);
                double m22_11 = (matrix[1][0] * matrix[3][2]) - (matrix[3][0] * matrix[1][2]);
                double m22_12 = (matrix[1][0] * matrix[3][1]) - (matrix[3][0] * matrix[1][1]);
                double m22_13 = (matrix[1][2] * matrix[2][3]) - (matrix[2][2] * matrix[1][3]);
                double m22_14 = (matrix[1][1] * matrix[2][3]) - (matrix[2][1] * matrix[1][3]);
                double m22_15 = (matrix[1][1] * matrix[2][2]) - (matrix[2][1] * matrix[1][2]);
                double m22_16 = (matrix[1][0] * matrix[2][3]) - (matrix[2][0] * matrix[1][3]);
                double m22_17 = (matrix[1][0] * matrix[2][2]) - (matrix[2][0] * matrix[1][2]);
                double m22_18 = (matrix[1][0] * matrix[2][1]) - (matrix[2][0] * matrix[1][1]);

                double d00 = ((matrix[1][1] * m22_1) - (matrix[1][2] * m22_2) + (matrix[1][3] * m22_3));
                double d01 = -((matrix[1][0] * m22_1) - (matrix[1][2] * m22_4) + (matrix[1][3] * m22_5));
                double d02 = ((matrix[1][0] * m22_2) - (matrix[1][1] * m22_4) + (matrix[1][3] * m22_6));
                double d03 = -((matrix[1][0] * m22_3) - (matrix[1][1] * m22_5) + (matrix[1][2] * m22_6));

                double det = (d00 * matrix[0][0]) + (d01 * matrix[0][1]) + (d02 * matrix[0][2]) + (d03 * matrix[0][3]);
                if (det == 0)
                {
                    throw new Exception("MatrixInverse44(): No inverse matrix exists");
                }

                inverse[0][0] = (float)(d00 / det);
                inverse[1][0] = (float)(d01 / det);
                inverse[2][0] = (float)(d02 / det);
                inverse[3][0] = (float)(d03 / det);
                inverse[0][1] = (float)(-((matrix[0][1] * m22_1) - (matrix[0][2] * m22_2) + (matrix[0][3] * m22_3)) / det);
                inverse[1][1] = (float)(((matrix[0][0] * m22_1) - (matrix[0][2] * m22_4) + (matrix[0][3] * m22_5)) / det);
                inverse[2][1] = (float)(-((matrix[0][0] * m22_2) - (matrix[0][1] * m22_4) + (matrix[0][3] * m22_6)) / det);
                inverse[3][1] = (float)(((matrix[0][0] * m22_3) - (matrix[0][1] * m22_5) + (matrix[0][2] * m22_6)) / det);
                inverse[0][2] = (float)(((matrix[0][1] * m22_7) - (matrix[0][2] * m22_8) + (matrix[0][3] * m22_9)) / det);
                inverse[1][2] = (float)(-((matrix[0][0] * m22_7) - (matrix[0][2] * m22_10) + (matrix[0][3] * m22_11)) / det);
                inverse[2][2] = (float)(((matrix[0][0] * m22_8) - (matrix[0][1] * m22_10) + (matrix[0][3] * m22_12)) / det);
                inverse[3][2] = (float)(-((matrix[0][0] * m22_9) - (matrix[0][1] * m22_11) + (matrix[0][2] * m22_12)) / det);
                inverse[0][3] = (float)(-((matrix[0][1] * m22_13) - (matrix[0][2] * m22_14) + (matrix[0][3] * m22_15)) / det);
                inverse[1][3] = (float)(((matrix[0][0] * m22_13) - (matrix[0][2] * m22_16) + (matrix[0][3] * m22_17)) / det);
                inverse[2][3] = (float)(-((matrix[0][0] * m22_14) - (matrix[0][1] * m22_16) + (matrix[0][3] * m22_18)) / det);
                inverse[3][3] = (float)(((matrix[0][0] * m22_15) - (matrix[0][1] * m22_17) + (matrix[0][2] * m22_18)) / det);

                return inverse;
            }

            public string build_x_file_skelleton(bone_node[] skelleton, bone_node[] root)
            {
                string ret_skelleton = "";
                System.Globalization.NumberFormatInfo nr_info = new System.Globalization.NumberFormatInfo();
                nr_info.NumberDecimalSeparator = ".";
                for (int i = 0; i < skelleton.Length; i++)
                {
                    ret_skelleton += "\n\n Frame ";
                    ret_skelleton += "BONE_" + skelleton[i].name;
                    ret_skelleton += "\n {";
                    ret_skelleton += "\n   FrameTransformMatrix";
                    ret_skelleton += "\n   {";
                    ret_skelleton += "\n      ";
                    for (int j = 0; j < 16; j++)
                    {
                        ret_skelleton += skelleton[i].transformation_matrix[j].ToString("0.000000", nr_info);
                        if (j != 15)
                        {
                            ret_skelleton += ", ";
                        }
                        else
                        {
                            ret_skelleton += ";;";
                        }
                    }
                    ret_skelleton += "\n   }";
                    //add child nodes here
                    if (skelleton[i].child_nodes != null)
                    {
                        ret_skelleton += build_x_file_skelleton(skelleton[i].child_nodes, root);
                    }
                    ret_skelleton += "\n }";
                }
                return ret_skelleton;
            }

            public string fast_collapse_string_array(string[] array)
            {
                string ret_string;
                int length = 0;
                for (int i = 0; i < array.Length; i++)
                {
                    length += array[i].Length;
                }
                char[] char_arr = new char[length];
                int offset = 0;
                for (int i = 0; i < array.Length; i++)
                {
                    for (int j = 0; j < array[i].Length; j++)
                    {
                        char_arr[offset++] = array[i][j];
                    }
                }
                ret_string = new string(char_arr);
                return ret_string;
            }

            public byte[] make_x_file()
            {
                scene scene_obj = this;
                byte[] x_file = null;
                /*x_file_header*/
                string x_file_header = "";
                x_file_header += "xof 0303txt 0032\n";
                x_file_header += "//!MOD_TSO\n";
                x_file_header += "\ntemplate XSkinMeshHeader {\n <3cf169ce-ff7c-44ab-93c0-f78f62d172e2>\n WORD nMaxSkinWeightsPerVertex;\n WORD nMaxSkinWeightsPerFace;\n WORD nBones;\n}";
                x_file_header += "\ntemplate SkinWeights {\n <6f0d123b-bad2-4167-a0d0-80224f25fabb>\n STRING transformNodeName;\n DWORD nWeights;\n array DWORD vertexIndices[nWeights];\n array FLOAT weights[nWeights];\n Matrix4x4 matrixOffset;\n}";
                x_file_header += "\ntemplate AnimTicksPerSecond {\n <9e415a43-7ba6-4a73-8743-b73d47e88476>\n DWORD AnimTicksPerSecond;\n}";
                x_file_header += "\ntemplate Frame {\n <3d82ab46-62da-11cf-ab39-0020af71e433>\n [...]\n}";
                x_file_header += "\ntemplate Matrix4x4 {\n <f6f23f45-7686-11cf-8f52-0040333594a3>\n array FLOAT matrix[16];\n}";
                x_file_header += "\ntemplate FrameTransformMatrix {\n <f6f23f41-7686-11cf-8f52-0040333594a3>\n Matrix4x4 frameMatrix;\n}";
                x_file_header += "\ntemplate Vector {\n <3d82ab5e-62da-11cf-ab39-0020af71e433>\n FLOAT x;\n FLOAT y;\n FLOAT z;\n}";
                x_file_header += "\ntemplate MeshFace {\n <3d82ab5f-62da-11cf-ab39-0020af71e433>\n DWORD nFaceVertexIndices;\n array DWORD faceVertexIndices[nFaceVertexIndices];\n}";
                x_file_header += "\ntemplate Mesh {\n <3d82ab44-62da-11cf-ab39-0020af71e433>\n DWORD nVertices;\n array Vector vertices[nVertices];\n DWORD nFaces;\n array MeshFace faces[nFaces];\n [...]\n}";
                x_file_header += "\ntemplate MeshNormals {\n <f6f23f43-7686-11cf-8f52-0040333594a3>\n DWORD nNormals;\n array Vector normals[nNormals];\n DWORD nFaceNormals;\n array MeshFace faceNormals[nFaceNormals];\n}";
                x_file_header += "\ntemplate Coords2d {\n <f6f23f44-7686-11cf-8f52-0040333594a3>\n FLOAT u;\n FLOAT v;\n}";
                x_file_header += "\ntemplate MeshTextureCoords {\n <f6f23f40-7686-11cf-8f52-0040333594a3>\n DWORD nTextureCoords;\n array Coords2d textureCoords[nTextureCoords];\n}";
                x_file_header += "\ntemplate ColorRGBA {\n <35ff44e0-6c7c-11cf-8f52-0040333594a3>\n FLOAT red;\n FLOAT green;\n FLOAT blue;\n FLOAT alpha;\n}";
                x_file_header += "\ntemplate ColorRGB {\n <d3e16e81-7835-11cf-8f52-0040333594a3>\n FLOAT red;\n FLOAT green;\n FLOAT blue;\n}";
                x_file_header += "\ntemplate Material {\n <3d82ab4d-62da-11cf-ab39-0020af71e433>\n ColorRGBA faceColor;\n FLOAT power;\n ColorRGB specularColor;\n ColorRGB emissiveColor;\n [...]\n}";
                x_file_header += "\ntemplate MeshMaterialList {\n <f6f23f42-7686-11cf-8f52-0040333594a3>\n DWORD nMaterials;\n DWORD nFaceIndexes;\n array DWORD faceIndexes[nFaceIndexes];\n [Material <3d82ab4d-62da-11cf-ab39-0020af71e433>]\n}";
                x_file_header += "\ntemplate TextureFilename {\n <a42790e1-7810-11cf-8f52-0040333594a3>\n STRING filename;\n}";
                x_file_header += "\ntemplate VertexDuplicationIndices {\n <b8d65549-d7c9-4995-89cf-53a9a8b031e3>\n DWORD nIndices;\n DWORD nOriginalVertices;\n array DWORD indices[nIndices];\n}";
                x_file_header += "\ntemplate Animation {\n <3d82ab4f-62da-11cf-ab39-0020af71e433>\n [...]\n}";
                x_file_header += "\ntemplate AnimationSet {\n <3d82ab50-62da-11cf-ab39-0020af71e433>\n [Animation <3d82ab4f-62da-11cf-ab39-0020af71e433>]\n}";
                x_file_header += "\ntemplate FloatKeys {\n <10dd46a9-775b-11cf-8f52-0040333594a3>\n DWORD nValues;\n array FLOAT values[nValues];\n}";
                x_file_header += "\ntemplate TimedFloatKeys {\n <f406b180-7b3b-11cf-8f52-0040333594a3>\n DWORD time;\n FloatKeys tfkeys;\n}";
                x_file_header += "\ntemplate AnimationKey {\n <10dd46a8-775b-11cf-8f52-0040333594a3>\n DWORD keyType;\n DWORD nKeys;\n array TimedFloatKeys keys[nKeys];\n}";
                /*end of header*/

                //preprocessing mesh list
                for (int i = 0; i < scene_obj.meshes.Length; i++)
                {
                    if (scene_obj.meshes[i].sub_mesh_count > 1)
                    {
                        int sub_meshes_cnt = (int)scene_obj.meshes[i].sub_mesh_count;
                        int[] flat_bone_list = new int[scene_obj.bone_node_count + 1];
                        for (int j = 0; j < flat_bone_list.Length; j++)
                        {
                            flat_bone_list[j] = -1;
                        }
                        for (int j = 0; j < scene_obj.meshes[i].bone_index_LUT_entry_count; j++)
                        {
                            flat_bone_list[scene_obj.meshes[i].bone_index_LUT[j]] = j;
                        }
                        for (int j = 1; j < sub_meshes_cnt; j++)
                        {
                            for (int h = 0; h < scene_obj.meshes[i + j].bone_index_LUT_entry_count; h++)
                            {
                                if (flat_bone_list[scene_obj.meshes[i + j].bone_index_LUT[h]] == -1)
                                {
                                    scene_obj.meshes[i].bone_index_LUT.Add(scene_obj.meshes[i + j].bone_index_LUT[h]);
                                    flat_bone_list[scene_obj.meshes[i + j].bone_index_LUT[h]] = scene_obj.meshes[i].bone_index_LUT.Count - 1;
                                    scene_obj.meshes[i].bone_index_LUT_entry_count++;
                                }
                            }
                            for (int h = 0; h < scene_obj.meshes[i + j].vertices.Length; h++)
                            {
                                for (int g = 0; g < scene_obj.meshes[i + j].vertices[h].bone_weight_entry_count; g++)
                                {
                                    UInt32 old_index_val = scene_obj.meshes[i + j].vertices[h].bone_weight_field[g].bone_index;
                                    scene_obj.meshes[i + j].vertices[h].bone_weight_field[g].bone_index = (UInt32)flat_bone_list[scene_obj.meshes[i + j].bone_index_LUT[(int)old_index_val]];
                                }
                            }
                            vertex_field[] new_vertex_field = new vertex_field[scene_obj.meshes[i].vertices.Length + scene_obj.meshes[i + j].vertices.Length];
                            for (int h = 0; h < scene_obj.meshes[i].vertices.Length; h++)
                            {
                                new_vertex_field[h] = scene_obj.meshes[i].vertices[h];
                            }
                            for (int h = 0; h < scene_obj.meshes[i + j].vertices.Length; h++)
                            {
                                new_vertex_field[h + scene_obj.meshes[i].vertices.Length] = scene_obj.meshes[i + j].vertices[h];
                            }
                            scene_obj.meshes[i].vertices = new_vertex_field;
                            scene_obj.meshes[i].vertex_count += scene_obj.meshes[i + j].vertex_count;
                        }
                        i += sub_meshes_cnt;
                    }
                }

                /*skelleton structure*/

                string skelleton = build_x_file_skelleton(scene_obj.skellettons, scene_obj.skellettons);
                string meshes = build_x_file_mesh();

                string x_file_str = x_file_header + "\n" + skelleton + "\n" + meshes;

                x_file = System.Text.Encoding.ASCII.GetBytes(x_file_str);

                return x_file;
            }

            public string build_x_file_mesh()
            {
                scene scene_obj = this;
                string ret_meshes = "";
                System.Globalization.NumberFormatInfo nr_info = new System.Globalization.NumberFormatInfo();
                nr_info.NumberDecimalSeparator = ".";
                mesh[] meshes = scene_obj.meshes;
                int skip_indices = 0;
                for (int i = 0; i < meshes.Length; i++)
                {
                    skip_indices = 0;
                    ret_meshes += "\n\n Frame " + "NR" + i.ToString("000") + "_" + meshes[i].name + "_sep_" + "M_E_S_H" + "_sep_" + meshes[i].unknown1.ToString() + "_sep_" + meshes[i].sub_mesh_count.ToString() + "_sep_" + meshes[i].unknown3.ToString() + "_sep_" + "\n {"
                                    + "\n   FrameTransformMatrix" + "\n   {" + "\n      ";
                    string mesh_transform = "";
                    for (int j = 0; j < 16; j++)
                    {
                        if (j != 15)
                        {
                            mesh_transform += meshes[i].transform_matrix[j].ToString("0.000000", nr_info) + ",";
                        }
                        else
                        {
                            mesh_transform += meshes[i].transform_matrix[j].ToString("0.000000", nr_info) + ";;";
                        }
                    }
                    ret_meshes += mesh_transform + "\n   }";
                    //add actual mesh info here

                    //erase vertex duplicates from vertex list...
                    List<vertex_pos_map> vertex_positions_map = new List<vertex_pos_map>();
                    int new_pos = 0;
                    for (int j = 0; j < meshes[i].vertex_count; j++)
                    {
                        bool is_in_list = false;
                        for (int h = 0; h < vertex_positions_map.Count; h++)
                        {
                            if (vertex_positions_map[h].unique_entry == 1)
                            {
                                if ((meshes[i].vertices[(vertex_positions_map[h]).mapped_index].position[0].Equals(meshes[i].vertices[j].position[0])) &&
                                     (meshes[i].vertices[(vertex_positions_map[h]).mapped_index].position[1].Equals(meshes[i].vertices[j].position[1])) &&
                                     (meshes[i].vertices[(vertex_positions_map[h]).mapped_index].position[2].Equals(meshes[i].vertices[j].position[2])))
                                {
                                    //now testing if bone weights are identical
                                    if ((meshes[i].vertices[(vertex_positions_map[h]).mapped_index].bone_weight_entry_count.Equals(meshes[i].vertices[j].bone_weight_entry_count)))
                                    {
                                        //same entry count for bone weights... but the actual bones can still be different...
                                        bool identical_bones = true;
                                        for (int k = 0; k < meshes[i].vertices[j].bone_weight_entry_count; k++)
                                        {
                                            if (!(meshes[i].vertices[(vertex_positions_map[h]).mapped_index].bone_weight_field[k].bone_index.Equals(meshes[i].vertices[j].bone_weight_field[k].bone_index)))
                                            {
                                                identical_bones = false;
                                            }
                                        }
                                        if (identical_bones)
                                        {
                                            //now testing for identical vertex normals and UVs
                                            bool equal_normal = true;
                                            bool equal_UVs = true;
                                            for (int k = 0; k < 3; k++)
                                            {
                                                if (!(meshes[i].vertices[(vertex_positions_map[h]).mapped_index].normal[k].Equals(meshes[i].vertices[j].normal[k])))
                                                {
                                                    equal_normal = false;
                                                    break;
                                                }
                                            }
                                            if (!(meshes[i].vertices[(vertex_positions_map[h]).mapped_index].UV[0].Equals(meshes[i].vertices[j].UV[0])))
                                            {
                                                equal_UVs = false;
                                            }
                                            if (!(meshes[i].vertices[(vertex_positions_map[h]).mapped_index].UV[1].Equals(meshes[i].vertices[j].UV[1])))
                                            {
                                                equal_UVs = false;
                                            }
                                            if (equal_normal && equal_UVs)
                                            {
                                                is_in_list = true;
                                                vertex_pos_map new_entry = new vertex_pos_map();
                                                new_entry.mapped_index = (vertex_positions_map[h]).mapped_index;
                                                new_entry.new_position = vertex_positions_map[h].new_position;
                                                new_entry.unique_entry = -1;
                                                vertex_positions_map.Add(new_entry);
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        if (!is_in_list)
                        {
                            vertex_pos_map new_entry = new vertex_pos_map();
                            new_entry.mapped_index = j;
                            new_entry.new_position = new_pos;
                            new_entry.unique_entry = 1;
                            vertex_positions_map.Add(new_entry);
                            new_pos++;
                        }
                    }

                    List<string> mesh_vertices = new List<string>();
                    for (int j = 0; j < meshes[i].vertex_count; j++)
                    {
                        if (vertex_positions_map[j].unique_entry == 1)
                        {

                            mesh_vertices.Add("\n      " + meshes[i].vertices[vertex_positions_map[j].mapped_index].position[0].ToString(nr_info) + ";"
                            + meshes[i].vertices[vertex_positions_map[j].mapped_index].position[1].ToString(nr_info)
                            + ";" + meshes[i].vertices[vertex_positions_map[j].mapped_index].position[2].ToString(nr_info) + ";,");
                        }
                        /*mesh_vertices.Add("\n      " + meshes[i].vertices[j].position[0].ToString(nr_info) + ";" + meshes[i].vertices[j].position[1].ToString(nr_info)
                                        + ";" + meshes[i].vertices[j].position[2].ToString(nr_info) + ";,");*/
                    }

                    if (mesh_vertices[mesh_vertices.Count - 1].Length - 1 > -1)
                    {
                        mesh_vertices[mesh_vertices.Count - 1] = mesh_vertices[mesh_vertices.Count - 1].Remove(mesh_vertices[mesh_vertices.Count - 1].Length - 1) + ";";
                    }
                    //ret_meshes += "\n   Mesh " + meshes[i].name + "\n   {" + "\n      " + meshes[i].vertex_count.ToString() + ";";
                    ret_meshes += "\n   Mesh " + meshes[i].name + "\n   {" + "\n      " + mesh_vertices.Count.ToString() + ";";
                    //List<string> mesh_vertices_orig_TSO = new List<string>();
                    /*for (int j = 0; j < meshes[i].vertex_count; j++)
                    {
                        /*if (vertex_positions_map[j].unique_entry == 1)
                        {

                            mesh_vertices.Add("\n      " + meshes[i].vertices[vertex_positions_map[j].mapped_index].position[0].ToString(nr_info) + ";"
                                        + meshes[i].vertices[vertex_positions_map[j].mapped_index].position[1].ToString(nr_info)
                                        + ";" + meshes[i].vertices[vertex_positions_map[j].mapped_index].position[2].ToString(nr_info) + ";,");
                        }*/
                    /*mesh_vertices_orig_TSO.Add("\n//!MOD_TSO      " + meshes[i].vertices[j].position[0].ToString(nr_info) + ";" + meshes[i].vertices[j].position[1].ToString(nr_info)
                                    + ";" + meshes[i].vertices[j].position[2].ToString(nr_info) + ";,");
                }*/
                    ret_meshes += fast_collapse_string_array(mesh_vertices.ToArray());
                    //ret_meshes += fast_collapse_string_array(mesh_vertices_orig_TSO.ToArray());
                    UInt32 face_count = 0;
                    string face_list = "";
                    int disjunct_cnt = 0;
                    List<int> face_cnts = new List<int>(); //only used when there are submeshes...
                    int mesh_end_cnt_verts = (int)meshes[i].vertex_count;
                    if (meshes[i].sub_mesh_count > 1)
                    {
                        //there are submeshes...
                        for (int j = 1; j < meshes[i].sub_mesh_count; j++)
                        {
                            mesh_end_cnt_verts -= (int)meshes[i + j].vertex_count;
                        }
                    }
                    bool mesh_border_special = false;
                    for (int j = 2; j < meshes[i].vertex_count; j++)
                    {
                        if (j == mesh_end_cnt_verts)
                        {
                            face_cnts.Add((int)face_count);
                            mesh_end_cnt_verts += (int)meshes[i + face_cnts.Count].vertex_count;
                            j += 2;
                            if (j % 2 == 0)
                            {
                                mesh_border_special = false;
                            }
                            else
                            {
                                mesh_border_special = true;
                            }
                        }
                        if (is_vertex_triple_disjunct(meshes[i].vertices[j - 2],
                                                     meshes[i].vertices[j - 1],
                                                     meshes[i].vertices[j]))
                        {
                            if (!((j % 2 == 0) ^ mesh_border_special))
                            {
                                face_count++;
                                face_list += "\n      " + "3;" + vertex_positions_map[j - 2].new_position.ToString() + ";"
                                            + vertex_positions_map[j - 1].new_position.ToString() + ";"
                                            + vertex_positions_map[j].new_position.ToString() + ";,";
                            }
                            else
                            {
                                face_count++;
                                face_list += "\n      " + "3;" + vertex_positions_map[j].new_position.ToString() + ";"
                                            + vertex_positions_map[j - 1].new_position.ToString() + ";"
                                            + vertex_positions_map[j - 2].new_position.ToString() + ";,";
                            }
                            /*if ((disjunct_cnt < 5) && (disjunct_cnt > 3))
                            {
                                if (face_count % 2 == 0)
                                {
                                    face_count++;
                                    face_list += "\n      " + "3;" + vertex_positions_map[j - 2].new_position.ToString() + ";"
                                                + vertex_positions_map[j - 1].new_position.ToString() + ";"
                                                + vertex_positions_map[j].new_position.ToString() + ";,";
                                }
                                else
                                {
                                    face_count++;
                                    face_list += "\n      " + "3;" + vertex_positions_map[j].new_position.ToString() + ";"
                                                + vertex_positions_map[j - 1].new_position.ToString() + ";"
                                                + vertex_positions_map[j - 2].new_position.ToString() + ";,";
                                }
                                disjunct_cnt = 0;
                            }
                            else if (disjunct_cnt > 4)
                            {
                                face_count++;
                                face_list += "\n      " + "3;" + vertex_positions_map[j - 2].new_position.ToString() + ";"
                                            + vertex_positions_map[j - 1].new_position.ToString() + ";"
                                            + vertex_positions_map[j].new_position.ToString() + ";,";

                                disjunct_cnt = 0;
                            }
                            else
                            {
                                if ((face_count + disjunct_cnt) % 2 == 0)
                                {
                                    face_count++;
                                    face_list += "\n      " + "3;" + vertex_positions_map[j - 2].new_position.ToString() + ";"
                                                + vertex_positions_map[j - 1].new_position.ToString() + ";"
                                                + vertex_positions_map[j].new_position.ToString() + ";,";
                                }
                                else
                                {
                                    face_count++;
                                    face_list += "\n      " + "3;" + vertex_positions_map[j].new_position.ToString() + ";"
                                                + vertex_positions_map[j - 1].new_position.ToString() + ";"
                                                + vertex_positions_map[j - 2].new_position.ToString() + ";,";
                                }
                                disjunct_cnt = 0;
                            }*/
                        }
                        else
                        {
                            disjunct_cnt++;
                        }
                    }
                    ret_meshes += "\n      " + face_count.ToString() + ";";

                    //each face is clockwise oriented...
                    ret_meshes += face_list.Substring(0, face_list.Length - 1) + ";" + "\n      MeshNormals" + "\n      {" + "\n        "
                                   + mesh_vertices.Count.ToString() + ";";
                    List<string> mesh_normals = new List<string>();
                    int offset_pos = 0;
                    for (int j = 0; j < meshes[i].vertex_count; j++)
                    {
                        if (vertex_positions_map[j].unique_entry == 1)
                        {
                            if (offset_pos == mesh_vertices.Count - 1)
                            {
                                mesh_normals.Add("\n        " + meshes[i].vertices[vertex_positions_map[j].mapped_index].normal[0].ToString(nr_info) + ";" + meshes[i].vertices[vertex_positions_map[j].mapped_index].normal[1].ToString(nr_info) + ";" + meshes[i].vertices[vertex_positions_map[j].mapped_index].normal[2].ToString(nr_info) + ";;");
                            }
                            else
                            {
                                mesh_normals.Add("\n        " + meshes[i].vertices[vertex_positions_map[j].mapped_index].normal[0].ToString(nr_info) + ";" + meshes[i].vertices[vertex_positions_map[j].mapped_index].normal[1].ToString(nr_info) + ";" + meshes[i].vertices[vertex_positions_map[j].mapped_index].normal[2].ToString(nr_info) + ";,");
                                offset_pos++;
                            }
                        }
                    }
                    offset_pos = 0;
                    /*for (int j = 0; j < meshes[i].vertex_count; j++)
                    {
                            mesh_normals.Add("\n//!MOD_TSO        " + meshes[i].vertices[j].normal[0].ToString(nr_info) + ";" + meshes[i].vertices[j].normal[1].ToString(nr_info) + ";" + meshes[i].vertices[j].normal[2].ToString(nr_info) + ";");
                    }*/
                    ret_meshes += fast_collapse_string_array(mesh_normals.ToArray()) + "\n        " + face_count.ToString() + ";" + face_list.Substring(0, face_list.Length - 1) + ";" + "\n      }" + "\n      MeshTextureCoords" + "\n      {" + "\n        " + mesh_vertices.Count.ToString() + ";";
                    List<string> mesh_uvs = new List<string>();
                    offset_pos = 0;
                    for (int j = 0; j < meshes[i].vertex_count; j++)
                    {
                        if (vertex_positions_map[j].unique_entry == 1)
                        {
                            if (offset_pos == mesh_vertices.Count - 1)
                            {
                                mesh_uvs.Add("\n        " + meshes[i].vertices[vertex_positions_map[j].mapped_index].UV[0].ToString(nr_info) + ";" + meshes[i].vertices[vertex_positions_map[j].mapped_index].UV[1].ToString(nr_info) + ";;");
                            }
                            else
                            {
                                mesh_uvs.Add("\n        " + meshes[i].vertices[vertex_positions_map[j].mapped_index].UV[0].ToString(nr_info) + ";" + meshes[i].vertices[vertex_positions_map[j].mapped_index].UV[1].ToString(nr_info) + ";,");
                                offset_pos++;
                            }
                        }
                    }
                    offset_pos = 0;
                    /*for (int j = 0; j < meshes[i].vertex_count; j++)
                    {
                        mesh_uvs.Add("\n//!MOD_TSO        " + meshes[i].vertices[j].UV[0].ToString(nr_info) + ";" + meshes[i].vertices[j].UV[1].ToString(nr_info) + ";");
                    }*/
                    //if there are sub_meshes, create a mesh material list, that encodes submeshes as materials
                    ret_meshes += fast_collapse_string_array(mesh_uvs.ToArray()) + "\n      }";
                    if (meshes[i].sub_mesh_count > 1)
                    {
                        //has submeshes
                        int true_submesh_cnt = (int)meshes[i].sub_mesh_count - 1;
                        ret_meshes += "\n      MeshMaterialList {\n        " + meshes[i].sub_mesh_count.ToString() + ";\n        " + face_count.ToString() + ";\n        ";
                        //now get the mesh face encoding
                        int current_material_index = 0;
                        string index_field = "";
                        for (int j = 0; j < face_count; j++)
                        {
                            if (current_material_index < face_cnts.Count)
                            {
                                if (j < face_cnts[current_material_index])
                                {
                                    index_field += current_material_index.ToString() + ",";
                                }
                                else
                                {
                                    current_material_index++;
                                    index_field += current_material_index.ToString() + ",";
                                }
                            }
                            else
                            {
                                //the last submesh face inidces
                                index_field += current_material_index.ToString() + ",";
                            }
                        }
                        index_field = index_field.Substring(0, index_field.Length - 1) + ";\n\n";
                        /*for (int j = 0; j < meshes[i].sub_mesh_count; j++)
                        {
                            index_field += "\n//!MOD_TSO       MeshBorders " + ((int)meshes[i + j].vertex_count).ToString();
                        }*/
                        ret_meshes += index_field + ";\n\n";
                        //now the face indices should be correctly mapped to material indices
                        //now the materials need to be defined...
                        for (int j = 0; j < meshes[i].sub_mesh_count; j++)
                        {
                            ret_meshes += "        Material " + "NR" + i.ToString("000") + "_" + meshes[i + j].name + "_sep_" + "M_A_T" + "_sep_" + meshes[i + j].unknown1.ToString() + "_sep_" + meshes[i + j].sub_mesh_count.ToString() + "_sep_" + meshes[i + j].unknown3.ToString() + "_sep_" + " {\n          0.400000;0.400000;0.400000;1.000000;;\n          32.000000;\n          0.700000;0.700000;0.700000;;\n          0.000000;0.000000;0.000000;;\n        }\n\n";
                        }
                        ret_meshes += "      }";
                        skip_indices = true_submesh_cnt;
                    }

                    string bone_LUT_entry_count = meshes[i].bone_index_LUT_entry_count.ToString();
                    ret_meshes += "\n      XSkinMeshHeader" + "\n      {" + "\n        " + bone_LUT_entry_count + ";" + "\n        " + bone_LUT_entry_count + ";" + "\n        " + bone_LUT_entry_count + ";" + "\n      }";

                    UInt32 offset = 0;
                    bone_node[] bones_flat = new bone_node[scene_obj.bone_node_count]; ;
                    flatten_skelleton(scene_obj.skellettons, ref offset, ref bones_flat);

                    for (int j = 0; j < meshes[i].bone_index_LUT_entry_count; j++)
                    {
                        UInt32 bone_LUT_index = meshes[i].bone_index_LUT[j];
                        if (offset < bone_LUT_index)
                        {
                            System.Console.Out.WriteLine("Warning: Invalid skinning information. DirectX Mesh skin might be incomplete.");
                            TSOdecrypt.Program.show_warnings = true;
                        }
                        else
                        {
                            bone_node bone = bones_flat[bone_LUT_index];
                            //everything okay, proceed
                            string bone_weight_list_weights = "";
                            string bone_weight_list_indexes = "";
                            UInt32 bone_weight_list_entries = 0;

                            for (int h = 0; h < meshes[i].vertex_count; h++)
                            {
                                if (vertex_positions_map[h].unique_entry == 1)
                                {
                                    for (int k = 0; k < meshes[i].vertices[vertex_positions_map[h].mapped_index].bone_weight_field.Length; k++)
                                    {
                                        if (meshes[i].vertices[vertex_positions_map[h].mapped_index].bone_weight_field[k].bone_index == j)
                                        {
                                            bone_weight_list_indexes += "\n        " + vertex_positions_map[h].new_position.ToString() + ",";
                                            bone_weight_list_weights += "\n        " + meshes[i].vertices[h].bone_weight_field[k].vertex_bone_weight.ToString("0.000000", nr_info) + ",";
                                            bone_weight_list_entries++;
                                        }
                                    }
                                }
                            }
                            if (bone_weight_list_indexes.Length - 1 > -1)
                                bone_weight_list_indexes = bone_weight_list_indexes.Remove(bone_weight_list_indexes.Length - 1);
                            
                            bone_weight_list_indexes += ";";

                            if (bone_weight_list_weights.Length - 1 > -1)
                                bone_weight_list_weights = bone_weight_list_weights.Remove(bone_weight_list_weights.Length - 1);

                            bone_weight_list_weights += ";";
                            /*for (int h = 0; h < meshes[i].vertex_count; h++)
                            {
                                for (int k = 0; k < meshes[i].vertices[h].bone_weight_field.Length; k++)
                                {
                                    if (meshes[i].vertices[h].bone_weight_field[k].bone_index == j)
                                    {
                                        bone_weight_list_indexes += "\n//!MOD_TSO        " + h.ToString() + ",";
                                        bone_weight_list_weights += "\n//!MOD_TSO        " + meshes[i].vertices[h].bone_weight_field[k].vertex_bone_weight.ToString("0.000000",nr_info) + ",";
                                    }
                                }
                            }
                            bone_weight_list_weights += "\n\n";*/
                            //now all vertice bone weights belonging to the same bone_node
                            //in a mesh should be listed
                            if (bone_weight_list_entries > 0)
                            {
                                ret_meshes += "\n      SkinWeights" + "\n      {" + "\n        " + "\"" + "BONE_" + bone.name + "\";" + "\n        " + bone_weight_list_entries.ToString() + ";" + bone_weight_list_indexes + ";" + bone_weight_list_weights + ";" + "\n        ";
                                //inverse traversal matrix of all bones along a path to the target bone 'bone'
                                Single[][] inverse_bone_matrix = get_bone_transform_matrix_from_skelleton(scene_obj.skellettons, bone);
                                string inverse_traversal_matrix = "";

                                for (int m = 0; m < 16; m++)
                                {
                                    if (m != 15)
                                    {
                                        inverse_traversal_matrix += inverse_bone_matrix[m / 4][m % 4].ToString("0.000000", nr_info) + ",";
                                    }
                                    else
                                    {
                                        inverse_traversal_matrix += inverse_bone_matrix[m / 4][m % 4].ToString("0.000000", nr_info) + ";;";
                                    }
                                }
                                ret_meshes += inverse_traversal_matrix + "\n      }";
                            }
                            else
                            {
                                System.Console.Out.WriteLine("Warning: There is useless skinning data. Discarding useless data to avoid any problems.");
                                TSOdecrypt.Program.show_warnings = true;
                            }
                        }
                    }
                    ret_meshes += "\n   }" + "\n }";
                    i += skip_indices;
                }
                return ret_meshes;
            }

            public byte[] x_file_data()
            {
                return make_x_file();
            }
        }

        public struct mesh
        {
            public string name;
            public Single[] transform_matrix; //16 entries
            public UInt32 unknown1;
            public UInt32 sub_mesh_count;
            public UInt32 unknown3;
            public UInt32 bone_index_LUT_entry_count;
            public List<UInt32> bone_index_LUT; //to look up bone field entries... bones are not directly assigned to vertices but by the means of this bone index LUT (look up table)... so if there is e.g. a bone field entry with the value 1, this means to look up in the LUT the first entry to retrieve the actual bone index...
            public UInt32 vertex_count;
            public vertex_field[] vertices;
        }

        public struct vertex_field
        {
            public Single[] position; //X,Y,Z
            public Single[] normal; //NX,NY,NZ
            public Single[] UV; //U,V
            public UInt32 bone_weight_entry_count;
            public bone_weight[] bone_weight_field;
        }

        public struct bone_weight
        {
            public UInt32 bone_index;
            public Single vertex_bone_weight;
        }

        public struct script
        {
            public string file_name;
            public string[] script_data;
            public script[] sub_scripts;
        }

        public struct texture
        {
            public string file_path; //usually the unquoted part of the entry
            public string file_name; //usually the name with quotations wrapped around it
            public byte[] data_stream;
        }

        public struct bone_node
        {
            public string name;
            public Single[] transformation_matrix; //16 entries
            public bone_node[] child_nodes;
        }
        public struct vertex_pos_map
        {
            public int mapped_index;
            public int new_position;
            public int unique_entry;
        }

        public int decrypt_TSO(string input_file, string dest_path)
        {
            try
            {
                reader = new System.IO.BinaryReader(System.IO.File.OpenRead(input_file));
            }
            catch (Exception)
            {
                System.Console.Out.WriteLine("Error: This file cannot be read or does not exist.");
                return -1;
            }

            byte[] file_header = new byte[4];
            file_header = reader.ReadBytes(4);

            if (!System.Text.Encoding.ASCII.GetString(file_header).Contains("TSO1"))
            {
                System.Console.Out.WriteLine("Error: This seems not to be a TSO file.");
            }
            scene scene_obj = new scene();

            UInt32 bone_entries = System.BitConverter.ToUInt32(reader.ReadBytes(4), 0);
            scene_obj.bone_node_count = bone_entries;
            UInt32 matrix_entries;

            while (true)
            {
                bone_node skelleton = read_skelleton(ref reader, "");
                if (skelleton.name == null)
                {
                    break;
                }
                if (scene_obj.skellettons == null)
                {
                    scene_obj.skellettons = new bone_node[1];
                    scene_obj.skellettons[0] = skelleton;
                }
                else
                {
                    bone_node[] t_skelletons = new bone_node[scene_obj.skellettons.Length + 1];
                    for (int i = 0; i < scene_obj.skellettons.Length; i++)
                    {
                        t_skelletons[i] = scene_obj.skellettons[i];
                    }
                    t_skelletons[scene_obj.skellettons.Length] = skelleton;
                    scene_obj.skellettons = t_skelletons;
                }
                matrix_entries = System.BitConverter.ToUInt32(reader.ReadBytes(4), 0);
                if (matrix_entries == bone_entries)
                {
                    break;
                }
                else
                {
                    reader.BaseStream.Position -= 4;
                }
            }
            read_skelleton_transform_matrices(ref reader, ref scene_obj.skellettons);

            UInt32 texture_count = System.BitConverter.ToUInt32(reader.ReadBytes(4), 0);
            scene_obj.textures = new texture[texture_count];
            for (int i = 0; i < texture_count; i++)
            {
                scene_obj.textures[i] = read_texture(ref reader);
                scene_obj.textures[i].file_path = "Tex" + i.ToString("000") + "_" + scene_obj.textures[i].file_path;
            }

            UInt32 script_count = System.BitConverter.ToUInt32(reader.ReadBytes(4), 0);
            scene_obj.scripts = new script[script_count];
            for (int i = 0; i < script_count; i++)
            {
                scene_obj.scripts[i] = read_script(ref reader);
            }

            mesh_binary_data = read_binary_mesh_data(reader);
            UInt32 mesh_count = System.BitConverter.ToUInt32(reader.ReadBytes(4), 0);
            scene_obj.meshes = new mesh[mesh_count];

            try
            {
                for (int i = 0; i < mesh_count; )
                {
                    mesh[] meshes = read_mesh(ref reader);
                    if (meshes.Length > 1)
                    {
                        //sub meshes make the current mesh array be too short, so it must be adjusted...
                        mesh[] temp_mesh_holder = new mesh[scene_obj.meshes.Length + meshes.Length - 1];
                        for (int j = 0; j < scene_obj.meshes.Length; j++)
                        {
                            temp_mesh_holder[j] = scene_obj.meshes[j];
                        }
                        scene_obj.meshes = temp_mesh_holder;
                        mesh_count += (UInt32)(meshes.Length - 1);
                    }
                    for (int j = 0; j < meshes.Length; j++)
                    {
                        scene_obj.meshes[i] = meshes[j];
                        i++;
                    }
                }
            }
            catch (Exception)
            {
                scene_obj.meshes = new mesh[0];
                System.Console.Out.WriteLine("WARNING: Could not parse mesh data from TSO");
                TSOdecrypt.Program.show_warnings = true;
            }

            write_out_data(scene_obj, dest_path);
            reader.Close();

            return 0;
        }

        public byte[] read_binary_mesh_data(System.IO.BinaryReader reader)
        {
            long old_pos = reader.BaseStream.Position;
            byte[] ret = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
            reader.BaseStream.Seek(old_pos, System.IO.SeekOrigin.Begin);
            return ret;
        }

        public void prepare_destination_directory(string file_name)
        {
            try
            {
                string[] dir_parts = file_name.Split(new string[] { "\\" }, System.StringSplitOptions.RemoveEmptyEntries);

                //test directories
                string test_directory = "";
                for (int l = 0; l < dir_parts.Length - 1; l++)
                {
                    test_directory += dir_parts[l] + "\\";
                    if (!System.IO.Directory.Exists(test_directory))
                    {
                        System.IO.Directory.CreateDirectory(test_directory);
                    }
                }
                //Does the file already exist?
                if (System.IO.File.Exists(file_name))
                {
                    System.IO.File.Delete(file_name);
                }
            }
            catch (Exception)
            {
                System.Console.Out.WriteLine("Error: Cannot prepare destination directory for file writing.");
                return;
            }
        }



        public void write_out_data(scene scene_obj, string dest_path)
        {
            //write textures
            for (int i = 0; i < scene_obj.textures.Length; i++)
            {
                string file_name = dest_path;
                file_name += "\\" + scene_obj.textures[i].file_path + "\\" + scene_obj.textures[i].file_name;
                prepare_destination_directory(file_name);
                System.IO.BinaryWriter file_writer = new System.IO.BinaryWriter(System.IO.File.Create(file_name));
                file_writer.Write(scene_obj.textures[i].data_stream);
            }
            //write cgfx files
            for (int i = 0; i < scene_obj.scripts.Length; i++)
            {
                string file_name = dest_path;
                file_name += "\\000_" + scene_obj.scripts[i].file_name;
                prepare_destination_directory(file_name);
                System.IO.BinaryWriter file_writer = new System.IO.BinaryWriter(System.IO.File.Create(file_name));
                for (int j = 0; j < scene_obj.scripts[i].script_data.Length; j++)
                {
                    file_writer.Write(System.Text.Encoding.ASCII.GetBytes(scene_obj.scripts[i].script_data[j] + "\n"));
                }
                file_writer.Close();
                for (int j = 0; j < scene_obj.scripts[i].sub_scripts.Length; j++)
                {
                    string sub_file_name = dest_path;
                    sub_file_name += "\\MAT" + j.ToString("000") + "_" + scene_obj.scripts[i].sub_scripts[j].file_name;
                    prepare_destination_directory(sub_file_name);
                    System.IO.BinaryWriter sub_file_writer = null;
                    try
                    {
                        sub_file_writer = new System.IO.BinaryWriter(System.IO.File.Create(sub_file_name));
                    }
                    catch (Exception)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            System.Threading.Thread.Sleep(1000);
                            try
                            {
                                sub_file_writer = new System.IO.BinaryWriter(System.IO.File.Create(sub_file_name));
                                break;
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }
                    try
                    {
                        for (int h = 0; h < scene_obj.scripts[i].sub_scripts[j].script_data.Length; h++)
                        {
                            sub_file_writer.Write(System.Text.Encoding.ASCII.GetBytes(scene_obj.scripts[i].sub_scripts[j].script_data[h] + "\n"));
                        }
                    }
                    catch (Exception)
                    {
                        System.Console.Out.WriteLine(String.Format("Warning: Could not write file: {0}. File skipped proceeding extraction now.", sub_file_name));
                        TSOdecrypt.Program.show_warnings = true;
                    }
                    sub_file_writer.Close();
                }
            }

            //write x-file
            string file_name_x = dest_path;
            string[] file_name_x_parts = dest_path.Split(new string[] { "\\" }, System.StringSplitOptions.RemoveEmptyEntries);
            file_name_x += "\\" + file_name_x_parts[file_name_x_parts.Length - 1];
            file_name_x += ".x";
            prepare_destination_directory(file_name_x);
            System.IO.BinaryWriter file_writer_x = new System.IO.BinaryWriter(System.IO.File.Create(file_name_x));
            try
            {
                file_writer_x.Write(scene_obj.x_file_data());
            }
            catch (Exception ex)
            {
                System.Console.Out.WriteLine(ex.ToString());
            }
            file_writer_x.Close();

            if (!this.parsed_meshes)
            {
                System.Console.Out.WriteLine("WARNING: Could not write mesh data on X file since meshes are unparsed from TSO (So the geometry of the mod cannot be edited using 3DSMAX)");
                TSOdecrypt.Program.show_warnings = true;
            }
            //write tso_mesh binary
            string file_name_bin = dest_path;
            string[] file_name_bin_parts = dest_path.Split(new string[] { "\\" }, System.StringSplitOptions.RemoveEmptyEntries);
            file_name_bin += "\\" + file_name_bin_parts[file_name_bin_parts.Length - 1];
            file_name_bin += ".bin";
            prepare_destination_directory(file_name_bin);
            System.IO.BinaryWriter file_writer_bin = new System.IO.BinaryWriter(System.IO.File.Create(file_name_bin));
            try
            {
                file_writer_bin.Write(mesh_binary_data);
            }
            catch (Exception ex)
            {
                System.Console.Out.WriteLine(ex.ToString());
            }
            file_writer_bin.Close();
            //thats it...
        }

        public mesh[] read_mesh(ref System.IO.BinaryReader reader)
        {
            mesh[] read_mesh;
            mesh act_mesh = new mesh();

            act_mesh.name = read_bytes_until_zero(ref reader, false);
            act_mesh.name = act_mesh.name.Replace(":", "_colon_"); //should be compatible with directx naming conventions 
            act_mesh.transform_matrix = new Single[16];
            for (int i = 0; i < 16; i++)
            {
                var x = reader.ReadBytes(4);
                act_mesh.transform_matrix[i] = System.BitConverter.ToSingle(x, 0);
            }
            act_mesh.unknown1 = System.BitConverter.ToUInt32(reader.ReadBytes(4), 0);
            act_mesh.sub_mesh_count = System.BitConverter.ToUInt32(reader.ReadBytes(4), 0);
            UInt32 sub_mesh_count = act_mesh.sub_mesh_count;
            read_mesh = new mesh[sub_mesh_count];
            for (int a = 0; a < sub_mesh_count; a++)
            {
                if (a > 0)
                {
                    //this is for sub meshes only...
                    act_mesh = new mesh();
                    act_mesh.name = read_mesh[0].name + "_sub_" + a.ToString();
                    act_mesh.transform_matrix = read_mesh[0].transform_matrix;
                    act_mesh.unknown1 = read_mesh[0].unknown1;
                    act_mesh.sub_mesh_count = 0;//read_mesh[0].sub_mesh_count;
                }
                act_mesh.unknown3 = System.BitConverter.ToUInt32(reader.ReadBytes(4), 0);
                act_mesh.bone_index_LUT_entry_count = System.BitConverter.ToUInt32(reader.ReadBytes(4), 0);
                act_mesh.bone_index_LUT = new List<UInt32>();
                for (int i = 0; i < act_mesh.bone_index_LUT_entry_count; i++)
                {
                    act_mesh.bone_index_LUT.Add(System.BitConverter.ToUInt32(reader.ReadBytes(4), 0));
                }
                act_mesh.vertex_count = System.BitConverter.ToUInt32(reader.ReadBytes(4), 0);
                act_mesh.vertices = new vertex_field[act_mesh.vertex_count];
                for (int i = 0; i < act_mesh.vertex_count; i++)
                {
                    act_mesh.vertices[i].position = new Single[3];
                    act_mesh.vertices[i].position[0] = System.BitConverter.ToSingle(reader.ReadBytes(4), 0);
                    act_mesh.vertices[i].position[1] = System.BitConverter.ToSingle(reader.ReadBytes(4), 0);
                    act_mesh.vertices[i].position[2] = System.BitConverter.ToSingle(reader.ReadBytes(4), 0);
                    act_mesh.vertices[i].normal = new Single[3];
                    act_mesh.vertices[i].normal[0] = System.BitConverter.ToSingle(reader.ReadBytes(4), 0);
                    act_mesh.vertices[i].normal[1] = System.BitConverter.ToSingle(reader.ReadBytes(4), 0);
                    act_mesh.vertices[i].normal[2] = System.BitConverter.ToSingle(reader.ReadBytes(4), 0);
                    act_mesh.vertices[i].UV = new Single[2];
                    act_mesh.vertices[i].UV[0] = System.BitConverter.ToSingle(reader.ReadBytes(4), 0);
                    act_mesh.vertices[i].UV[1] = System.BitConverter.ToSingle(reader.ReadBytes(4), 0);
                    act_mesh.vertices[i].bone_weight_entry_count = System.BitConverter.ToUInt32(reader.ReadBytes(4), 0);
                    act_mesh.vertices[i].bone_weight_field = new bone_weight[act_mesh.vertices[i].bone_weight_entry_count];
                    for (int j = 0; j < act_mesh.vertices[i].bone_weight_entry_count; j++)
                    {
                        act_mesh.vertices[i].bone_weight_field[j].bone_index = System.BitConverter.ToUInt32(reader.ReadBytes(4), 0);
                        act_mesh.vertices[i].bone_weight_field[j].vertex_bone_weight = System.BitConverter.ToSingle(reader.ReadBytes(4), 0);
                    }
                }
                read_mesh[a] = act_mesh;
            }

            return read_mesh;
        }

        public script read_script(ref System.IO.BinaryReader reader)
        {
            script read_script = new script();
            string script_name = read_bytes_until_zero(ref reader, false);
            UInt32 line_count = System.BitConverter.ToUInt32(reader.ReadBytes(4), 0);
            string[] read_lines = new string[line_count];
            for (int i = 0; i < line_count; i++)
            {
                read_lines[i] = read_bytes_until_zero(ref reader, true);
            }
            UInt32 sub_script_count = System.BitConverter.ToUInt32(reader.ReadBytes(4), 0);
            script[] sub_scripts = new script[sub_script_count];
            for (int i = 0; i < sub_script_count; i++)
            {
                sub_scripts[i].file_name = read_bytes_until_zero(ref reader, false);
                sub_scripts[i].file_name += "\\" + read_bytes_until_zero(ref reader, false);
                UInt32 sub_line_counts = System.BitConverter.ToUInt32(reader.ReadBytes(4), 0);
                sub_scripts[i].script_data = new string[sub_line_counts];
                for (int j = 0; j < sub_line_counts; j++)
                {
                    sub_scripts[i].script_data[j] = read_bytes_until_zero(ref reader, true);
                }
            }
            read_script.file_name = script_name;
            read_script.script_data = read_lines;
            read_script.sub_scripts = sub_scripts;
            return read_script;
        }

        public texture read_texture(ref System.IO.BinaryReader reader)
        {
            texture tex = new texture();

            string file_path = read_bytes_until_zero(ref reader, false);
            string file_name = read_bytes_until_zero(ref reader, false);
            file_name = file_name.Replace("\"", "");

            if (file_name == "")
            {
                System.Console.Out.WriteLine("Warning: invalid texture " + file_path + ". Maybe you want remove that from output folder");
                TSOdecrypt.Program.show_warnings = true;
                file_name = "texture.bmp";
            }
            UInt32 width = System.BitConverter.ToUInt32(reader.ReadBytes(4), 0);
            UInt32 height = System.BitConverter.ToUInt32(reader.ReadBytes(4), 0);
            UInt32 channels = System.BitConverter.ToUInt32(reader.ReadBytes(4), 0);
            byte[] texture_data = null;

            if ((file_name.Substring(file_name.Length - 3).ToLower().CompareTo("bmp") == 0))
            {
                /* create a bmp header */
                byte[] bmp_header = new byte[54];
                byte[] width_bytes = new byte[4];
                byte[] height_bytes = new byte[4];
                byte[] file_size_bytes = new byte[4];
                byte[] bits_per_pixel_bytes = new byte[2];
                byte[] img_data_size_bytes = new byte[4];
                byte[] pixel_data = new byte[channels];
                UInt32 img_data_size = width * height * channels;
                UInt32 file_size = img_data_size + 54;
                UInt16 bits_per_pixel = (UInt16)(channels * 8);
                texture_data = new byte[file_size];


                file_size_bytes = System.BitConverter.GetBytes(file_size);
                width_bytes = System.BitConverter.GetBytes(width);
                height_bytes = System.BitConverter.GetBytes(height);
                bits_per_pixel_bytes = System.BitConverter.GetBytes(bits_per_pixel);
                img_data_size_bytes = System.BitConverter.GetBytes(img_data_size);

                bmp_header.Initialize();
                bmp_header[0] = 0x42; bmp_header[1] = 0x4D;
                bmp_header[2] = file_size_bytes[0];
                bmp_header[3] = file_size_bytes[1];
                bmp_header[4] = file_size_bytes[2];
                bmp_header[5] = file_size_bytes[3];
                bmp_header[10] = 0x36; bmp_header[14] = 0x28;
                bmp_header[18] = width_bytes[0];
                bmp_header[19] = width_bytes[1];
                bmp_header[20] = width_bytes[2];
                bmp_header[21] = width_bytes[3];
                bmp_header[22] = height_bytes[0];
                bmp_header[23] = height_bytes[1];
                bmp_header[24] = height_bytes[2];
                bmp_header[25] = height_bytes[3];
                bmp_header[26] = 0x01;
                bmp_header[28] = bits_per_pixel_bytes[0];
                bmp_header[29] = bits_per_pixel_bytes[1];
                bmp_header[34] = img_data_size_bytes[0];
                bmp_header[35] = img_data_size_bytes[1];
                bmp_header[36] = img_data_size_bytes[2];
                bmp_header[37] = img_data_size_bytes[3];
                bmp_header[38] = 0x88; bmp_header[39] = 0x0B;
                bmp_header[42] = 0x88; bmp_header[43] = 0x0B;
                /* header ready for usage */
                for (int i = 0; i < 54; i++)
                {
                    texture_data[i] = bmp_header[i];
                }

                if (channels == 4)
                {
                    for (int i = 54; i < file_size; i += 4)
                    {
                        pixel_data = reader.ReadBytes(4);
                        texture_data[i] = pixel_data[2];
                        texture_data[i + 1] = pixel_data[1];
                        texture_data[i + 2] = pixel_data[0];
                        texture_data[i + 3] = pixel_data[3];
                    }
                }
                else if (channels == 3)
                {
                    for (int i = 54; i < file_size; i += 3)
                    {
                        pixel_data = reader.ReadBytes(3);
                        texture_data[i] = pixel_data[2];
                        texture_data[i + 1] = pixel_data[1];
                        texture_data[i + 2] = pixel_data[0];
                    }
                }
                else
                {
                    for (int i = 54; i < file_size; i++)
                    {
                        texture_data[i] = reader.ReadByte();
                    }
                }
            }
            else if ((file_name.Substring(file_name.Length - 3).ToLower().CompareTo("tga") == 0))
            {
                /* create a tga header */
                byte[] tga_header = new byte[18];
                byte[] width_bytes = new byte[2];
                byte[] height_bytes = new byte[2];
                byte[] bits_per_pixel_bytes = new byte[1];
                byte[] pixel_data = new byte[channels];
                UInt32 file_size = width * height * channels + 18;
                byte bits_per_pixel = (byte)(channels * 8);
                texture_data = new byte[file_size];
                width_bytes = System.BitConverter.GetBytes(((UInt16)width));
                height_bytes = System.BitConverter.GetBytes(((UInt16)height));
                bits_per_pixel_bytes = System.BitConverter.GetBytes(bits_per_pixel);

                tga_header.Initialize();
                tga_header[2] = 0x02;
                tga_header[12] = width_bytes[0];
                tga_header[13] = width_bytes[1];
                tga_header[14] = height_bytes[0];
                tga_header[15] = height_bytes[1];
                tga_header[16] = bits_per_pixel_bytes[0];
                /* header ready for usage */
                for (int i = 0; i < 18; i++)
                {
                    texture_data[i] = tga_header[i];
                }

                if (channels == 4)
                {
                    for (int i = 18; i < file_size; i += 4)
                    {
                        pixel_data = reader.ReadBytes(4);
                        texture_data[i] = pixel_data[2];
                        texture_data[i + 1] = pixel_data[1];
                        texture_data[i + 2] = pixel_data[0];
                        texture_data[i + 3] = pixel_data[3];
                    }
                }
                else if (channels == 3)
                {
                    for (int i = 18; i < file_size; i += 3)
                    {
                        pixel_data = reader.ReadBytes(3);
                        texture_data[i] = pixel_data[2];
                        texture_data[i + 1] = pixel_data[1];
                        texture_data[i + 2] = pixel_data[0];
                    }
                }
                else
                {
                    for (int i = 18; i < file_size; i++)
                    {
                        texture_data[i] = reader.ReadByte();
                    }
                }
            }
            else
            {
                System.Console.Out.WriteLine("This texture format is not supported.\nDefaulting to BMP format.");
                System.Console.Out.WriteLine("Message the author of this tool for support.\nIf the file name does not have an extension,\nthen the original file format is unknown.\nIn that case do not bother to message the author. ;)");
                //assume bmp format
                // create a bmp header
                byte[] bmp_header = new byte[54];
                byte[] width_bytes = new byte[4];
                byte[] height_bytes = new byte[4];
                byte[] file_size_bytes = new byte[4];
                byte[] bits_per_pixel_bytes = new byte[2];
                byte[] img_data_size_bytes = new byte[4];
                byte[] pixel_data = new byte[channels];
                UInt32 img_data_size = width * height * channels;
                UInt32 file_size = img_data_size + 54;
                UInt16 bits_per_pixel = (UInt16)(channels * 8);
                texture_data = new byte[file_size];


                file_size_bytes = System.BitConverter.GetBytes(file_size);
                width_bytes = System.BitConverter.GetBytes(width);
                height_bytes = System.BitConverter.GetBytes(height);
                bits_per_pixel_bytes = System.BitConverter.GetBytes(bits_per_pixel);
                img_data_size_bytes = System.BitConverter.GetBytes(img_data_size);

                bmp_header.Initialize();
                bmp_header[0] = 0x42; bmp_header[1] = 0x4D;
                bmp_header[2] = file_size_bytes[0];
                bmp_header[3] = file_size_bytes[1];
                bmp_header[4] = file_size_bytes[2];
                bmp_header[5] = file_size_bytes[3];
                bmp_header[10] = 0x36; bmp_header[14] = 0x28;
                bmp_header[18] = width_bytes[0];
                bmp_header[19] = width_bytes[1];
                bmp_header[20] = width_bytes[2];
                bmp_header[21] = width_bytes[3];
                bmp_header[22] = height_bytes[0];
                bmp_header[23] = height_bytes[1];
                bmp_header[24] = height_bytes[2];
                bmp_header[25] = height_bytes[3];
                bmp_header[26] = 0x01;
                bmp_header[28] = bits_per_pixel_bytes[0];
                bmp_header[29] = bits_per_pixel_bytes[1];
                bmp_header[34] = img_data_size_bytes[0];
                bmp_header[35] = img_data_size_bytes[1];
                bmp_header[36] = img_data_size_bytes[2];
                bmp_header[37] = img_data_size_bytes[3];
                bmp_header[38] = 0x88; bmp_header[39] = 0x0B;
                bmp_header[42] = 0x88; bmp_header[43] = 0x0B;
                // header ready for usage 
                for (int i = 0; i < 54; i++)
                {
                    texture_data[i] = bmp_header[i];
                }

                if (channels == 4)
                {
                    for (int i = 54; i < file_size; i += 4)
                    {
                        pixel_data = reader.ReadBytes(4);
                        texture_data[i] = pixel_data[2];
                        texture_data[i + 1] = pixel_data[1];
                        texture_data[i + 2] = pixel_data[0];
                        texture_data[i + 3] = pixel_data[3];
                    }
                }
                else if (channels == 3)
                {
                    for (int i = 54; i < file_size; i += 3)
                    {
                        pixel_data = reader.ReadBytes(3);
                        texture_data[i] = pixel_data[2];
                        texture_data[i + 1] = pixel_data[1];
                        texture_data[i + 2] = pixel_data[0];
                    }
                }
                else
                {
                    for (int i = 54; i < file_size; i++)
                    {
                        texture_data[i] = reader.ReadByte();
                    }
                }

            }
            tex.file_name = file_name;
            tex.file_path = file_path;
            tex.data_stream = texture_data;
            return tex;
        }

        public int read_skelleton_transform_matrices(ref System.IO.BinaryReader reader, ref bone_node[] skelletons)
        {
            int ret = 0;
            if (skelletons != null)
            {
                for (int i = 0; i < skelletons.Length; i++)
                {
                    skelletons[i].transformation_matrix = new Single[16];
                    try
                    {
                        for (int j = 0; j < 16; j++)
                        {
                            skelletons[i].transformation_matrix[j] = System.BitConverter.ToSingle(reader.ReadBytes(4), 0);
                        }
                    }
                    catch (Exception)
                    {
                        System.Console.Out.WriteLine("Error: Unexpected condition in TSO file.");
                        return -1;
                    }
                    if (skelletons[i].child_nodes != null)
                    {
                        ret = read_skelleton_transform_matrices(ref reader, ref skelletons[i].child_nodes);
                    }
                }
            }
            return ret;
        }

        //use empty root_name when calling from outside this function
        public bone_node read_skelleton(ref System.IO.BinaryReader reader, string parent_name)
        {
            bone_node root = new bone_node();
            string hierarchy = read_bytes_until_zero(ref reader, false);
            if (hierarchy == null)
            {
                return root;
            }
            if (hierarchy.CompareTo("") == 0)
            {
                return root;
            }
            if (!hierarchy[0].Equals('|'))
            {
                //reached the end of the description field for bones
                //reset the reader position
                long get_pos = reader.BaseStream.Position;
                get_pos -= (hierarchy.Length + 1);
                reader.BaseStream.Position = get_pos;
                //now return with the empty root
                return root;
            }
            string[] parse_hierarchy = hierarchy.Split(new string[] { "|" }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parse_hierarchy.Length == 1)
            {
                //this is a root entry, thus root_name must be null
                //otherwise the current skelleton ended here
                if (parent_name.CompareTo("") == 0)
                {
                    root.name = parse_hierarchy[0];
                }
                else
                {
                    //reset the reader position
                    long get_pos = reader.BaseStream.Position;
                    get_pos -= (hierarchy.Length + 1);
                    reader.BaseStream.Position = get_pos;
                    //now return with the empty root
                    return root;
                }
            }
            else if (parse_hierarchy[parse_hierarchy.Length - 2].CompareTo(parent_name) == 0)
            {
                //a valid child node
                root.name = parse_hierarchy[parse_hierarchy.Length - 1];
            }
            else
            {
                //this is not a child, return with
                //empty root but reset reader first
                //reset the reader position
                long get_pos = reader.BaseStream.Position;
                get_pos -= (hierarchy.Length + 1);
                reader.BaseStream.Position = get_pos;
                //now return with the empty root
                return root;
            }

            while (true)
            {
                bone_node child = read_skelleton(ref reader, root.name);
                if (child.name != null)
                {
                    //a valid child entry returned
                    if (root.child_nodes == null)
                    {
                        root.child_nodes = new bone_node[1];
                        root.child_nodes[0] = child;
                    }
                    else
                    {
                        bone_node[] children = new bone_node[root.child_nodes.Length + 1];
                        for (int i = 0; i < root.child_nodes.Length; i++)
                        {
                            children[i] = root.child_nodes[i];
                        }
                        children[root.child_nodes.Length] = child;
                        root.child_nodes = children;
                    }
                }
                else
                {
                    //this is not a valid child entry... return
                    return root;
                }
            }
        }

        public string read_bytes_until_zero(ref System.IO.BinaryReader reader, bool override_reader_reset)
        {
            string ret_string = "";
            byte[] read_byte = new byte[1];
            //while ((read_byte[0] = reader.ReadByte()) != 0x00)

            try
            {
                while (true)
                {
                    read_byte[0] = reader.ReadByte();
                    if (read_byte[0] == 0x00) break;

                    ret_string += System.Text.Encoding.ASCII.GetString(read_byte);
                }
            } catch (Exception e) {
            }
            if ((ret_string.CompareTo("") == 0) && !override_reader_reset)
            {
                reader.BaseStream.Position -= 1;
            }
            return ret_string;
        }
    }

}
