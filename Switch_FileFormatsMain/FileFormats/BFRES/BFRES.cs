﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Data;
using System.Threading;
using System.Windows.Forms;
using Switch_Toolbox.Library;
using Switch_Toolbox.Library.Forms;
using Switch_Toolbox.Library.IO;
using Bfres.Structs;
using ResU = Syroot.NintenTools.Bfres;
using Syroot.NintenTools.NSW.Bfres;
using Switch_Toolbox.Library.Animations;
using Switch_Toolbox.Library.NodeWrappers;
using GL_EditorFramework.Interfaces;
using FirstPlugin.Forms;
using FirstPlugin.NodeWrappers;

namespace FirstPlugin
{
    public class BFRES : BFRESWrapper, IFileFormat
    {
        public bool CanSave { get; set; }
        public string[] Description { get; set; } = new string[] { "BFRES" };
        public string[] Extension { get; set; } = new string[] {
            "*.bfres", "*.sbfres", "*.sbmapopen", "*.sbstftex", "*.sbitemico", "*.sbmaptex", "*.sbreviewtex" };
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public IFileInfo IFileInfo { get; set; }

        public bool Identify(Stream stream)
        {
            using (var reader = new FileReader(stream, true))
            {
                return reader.CheckSignature(4, "FRES");
            }
        }

        public Type[] Types
        {
            get
            {
                List<Type> types = new List<Type>();
                types.Add(typeof(MenuExt));
                return types.ToArray();
            }
        }

        private Thread Thread;

        public override string ExportFilter => Utils.GetAllFilters(new BFRES());

        class MenuExt : IFileMenuExtension
        {
            public STToolStripItem[] NewFileMenuExtensions => newFileExt;
            public STToolStripItem[] NewFromFileMenuExtensions => null;
            public STToolStripItem[] EditMenuExtensions => editExt;
            public STToolStripItem[] ToolsMenuExtensions => toolExt;
            public STToolStripItem[] TitleBarExtensions => null;
            public STToolStripItem[] CompressionMenuExtensions => null;
            public STToolStripItem[] ExperimentalMenuExtensions => null;
            public ToolStripButton[] IconButtonMenuExtensions => null;

            STToolStripItem[] toolExt = new STToolStripItem[1];
            STToolStripItem[] newFileExt = new STToolStripItem[2];
            STToolStripItem[] editExt = new STToolStripItem[1];

            public MenuExt()
            {
                editExt[0] = new STToolStripItem("Use Advanced Editor As Default", AdvancedEditor);
                toolExt[0] = new STToolStripItem("Open Bfres Debugger", DebugInfo);
                newFileExt[0] = new STToolStripItem("BFRES (Switch)", NewSwitchBfres);
                newFileExt[1] = new STToolStripItem("BFRES (Wii U)", NewWiiUBfres);

                editExt[0].Checked = !PluginRuntime.UseSimpleBfresEditor;
            }

            private void AdvancedEditor(object sender, EventArgs args)
            {
                BFRES file = null;

                ObjectEditor editor = (ObjectEditor)LibraryGUI.Instance.GetActiveForm();
                if (editor != null)
                {
                    file = (BFRES)editor.GetActiveFile();                }

                if (editExt[0].Checked)
                {
                    editExt[0].Checked = false;
                    PluginRuntime.UseSimpleBfresEditor = true;

                    if (file != null)
                        file.LoadSimpleMode();
                }
                else
                {
                    editExt[0].Checked = true;
                    PluginRuntime.UseSimpleBfresEditor = false;

                    if (file != null)
                        file.LoadAdvancedMode();
                }
            }
            private void NewWiiUBfres(object sender, EventArgs args)
            {
                BFRES bfres = new BFRES();
                bfres.IFileInfo = new IFileInfo();
                bfres.FileName = "Untitled.bfres";

                bfres.Load(new MemoryStream(BfresWiiU.CreateNewBFRES("Untitled.bfres")));

                ObjectEditor editor = new ObjectEditor();
                editor.Text = "Untitled-" + 0;
                editor.treeViewCustom1.Nodes.Add(bfres);
                LibraryGUI.Instance.CreateMdiWindow(editor);
            }
            private void NewSwitchBfres(object sender, EventArgs args)
            {
                BFRES bfres = new BFRES();
                bfres.IFileInfo = new IFileInfo();
                bfres.FileName = "Untitled.bfres";

                bfres.Load(new MemoryStream(CreateNewBFRESSwitch("Untitled.bfres")));

                ObjectEditor editor = new ObjectEditor();
                editor.Text = "Untitled-" + 0;
                editor.treeViewCustom1.Nodes.Add(bfres);
                LibraryGUI.Instance.CreateMdiWindow(editor);
            }
            private void DebugInfo(object sender, EventArgs args)
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Filter = Utils.GetAllFilters(new BFRES());

                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                var debugInfo = new DebugInfoBox();
                debugInfo.Show();
                debugInfo.PrintDebugInfo(ofd.FileName);
            }
        }

        private static byte[] CreateNewBFRESSwitch(string Name)
        {
            MemoryStream mem = new MemoryStream();

            ResFile resFile = new ResFile();
            resFile.Name = Name;

            resFile.Save(mem);
            var data = mem.ToArray();

            mem.Close();
            mem.Dispose();
            return data;
        }



        public void OnPropertyChanged()
        {
            if (resFile != null)
            {
                Text = resFile.Name;
            }
            else
            {
                Text = resFileU.Name;
            }
            STPropertyGrid editor = (STPropertyGrid)LibraryGUI.Instance.GetActiveContent(typeof(STPropertyGrid));

            if (editor != null)
                editor.Refresh();
        }

        //Used for adding new skeleton drawables
        public void AddSkeletonDrawable(STSkeleton skeleton)
        {
            BfresEditor bfresEditor = (BfresEditor)LibraryGUI.Instance.GetActiveContent(typeof(BfresEditor));
            if (bfresEditor != null)
            {
                bfresEditor.AddDrawable(skeleton);
            }

            drawables.Add(skeleton);
        }

        public void RemoveSkeletonDrawable(STSkeleton skeleton)
        {
            BfresEditor bfresEditor = (BfresEditor)LibraryGUI.Instance.GetActiveContent(typeof(BfresEditor));
            if (bfresEditor != null)
            {
                bfresEditor.RemoveDrawable(skeleton);
            }

            drawables.Remove(skeleton);
        }

        public void LoadAdvancedMode()
        {
            foreach (var model in BFRESRender.models)
            {
                foreach (var mat in model.materials.Values)
                {
                    foreach (var tex in mat.TextureMaps)
                    {
                        mat.Nodes.RemoveByKey(tex.Name);
                    }
                }
            }
        }

        public void LoadSimpleMode()
        {
            ObjectEditor editor = (ObjectEditor)LibraryGUI.Instance.GetActiveForm();
            if (editor == null)
                return;

            editor.treeViewCustom1.BeginUpdate();

            foreach (var model in BFRESRender.models)
            {
                foreach (var mat in model.materials.Values)
                {
                    mat.Nodes.Clear();

                    foreach (MatTexture tex in mat.TextureMaps)
                    {
                        mat.Nodes.Add(new MatTextureWrapper(tex.Name, tex.Name, tex));
                    }
                }
            }

            foreach (TreeNode node in Nodes)
            {
               if (node is BFRESAnimFolder)
                {
                    foreach (BFRESGroupNode animFolder in node.Nodes)
                    {
                        if (animFolder.Type == BRESGroupType.SkeletalAnim)
                        {
                            foreach (FSKA anim in animFolder.Nodes)
                            {
                                foreach (FSKA.BoneAnimNode bone in ((FSKA)anim).Bones)
                                {
                                    int index = 0;
                                    if (bone.BoneAnimU != null)
                                    {
                                        foreach (var curve in bone.BoneAnimU.Curves)
                                            GetSkeletonAnimCurveOffset(curve.AnimDataOffset);
                                    }
                                    else
                                    {
                                        foreach (var curve in bone.BoneAnim.Curves)
                                            GetSkeletonAnimCurveOffset(curve.AnimDataOffset);
                                    }
                                }
                            }
                        }
                        if (animFolder.Type == BRESGroupType.ColorAnim)
                        {
                            foreach (var anim in animFolder.Nodes)
                            {
                                if (anim is FMAA)
                                {
                                    foreach (FMAA.MaterialAnimEntry mat in ((FSKA)anim).Nodes)
                                    {

                                    }
                                }
                            }
                        }
                    }
                }
            }

            editor.treeViewCustom1.EndUpdate();
        }

        private string GetSkeletonAnimCurveOffset(uint offset)
        {
            switch ((FSKA.TrackType)offset)
            {
                case FSKA.TrackType.XPOS: return "Translate_X";
                case FSKA.TrackType.YPOS: return "Translate_Y";
                case FSKA.TrackType.ZPOS: return "Translate_Z";
                case FSKA.TrackType.XROT: return "Rotate_X";
                case FSKA.TrackType.YROT: return "Rotate_Y";
                case FSKA.TrackType.ZROT: return "Rotate_Z";
                case FSKA.TrackType.WROT: return "Rotate_W";
                case FSKA.TrackType.XSCA: return "Scale_X";
                case FSKA.TrackType.YSCA: return "Scale_Y";
                case FSKA.TrackType.ZSCA: return "Scale_Z";
                default:  offset.ToString(); break;
            }

            return "";
        }

        List<AbstractGlDrawable> drawables = new List<AbstractGlDrawable>();
        public void LoadEditors(object SelectedSection)
        {
            BfresEditor bfresEditor = (BfresEditor)LibraryGUI.Instance.GetActiveContent(typeof(BfresEditor));
            bool HasModels = BFRESRender.models.Count > 0;

            if (bfresEditor == null)
            {
                bfresEditor = new BfresEditor(HasModels);
                bfresEditor.Dock = DockStyle.Fill;
                LibraryGUI.Instance.LoadEditor(bfresEditor);
            }

            if (SelectedSection is FTEX)
            {
                ImageEditorBase editorFtex = (ImageEditorBase)bfresEditor.GetActiveEditor(typeof(ImageEditorBase));
                if (editorFtex == null)
                {
                    editorFtex = new ImageEditorBase();
                    editorFtex.Dock = DockStyle.Fill;

                    bfresEditor.LoadEditor(editorFtex);
                }
                editorFtex.Text = Text;
                editorFtex.LoadProperties(((FTEX)SelectedSection).texture);
                editorFtex.LoadImage((FTEX)SelectedSection);
                if (Runtime.DisplayViewport)
                    editorFtex.SetEditorOrientation(true);

                if (((FTEX)SelectedSection).texture.UserData != null)
                {
                    UserDataEditor userEditor = (UserDataEditor)editorFtex.GetActiveTabEditor(typeof(UserDataEditor));
                    if (userEditor == null)
                    {
                        userEditor = new UserDataEditor();
                        userEditor.Name = "User Data";
                        editorFtex.AddCustomControl(userEditor, typeof(UserDataEditor));
                    }
                    userEditor.LoadUserData(((FTEX)SelectedSection).texture.UserData);
                }
                return;
            }

            if (SelectedSection is TextureData)
            {
                ImageEditorBase editor = (ImageEditorBase)bfresEditor.GetActiveEditor(typeof(ImageEditorBase));
                if (editor == null)
                {
                    editor = new ImageEditorBase();
                    editor.Dock = DockStyle.Fill;
                    bfresEditor.LoadEditor(editor);
                }
                if (((TextureData)SelectedSection).Texture.UserData != null)
                {
                    UserDataEditor userEditor = (UserDataEditor)editor.GetActiveTabEditor(typeof(UserDataEditor));
                    if (userEditor == null)
                    {
                        userEditor = new UserDataEditor();
                        userEditor.Name = "User Data";
                        editor.AddCustomControl(userEditor, typeof(UserDataEditor));
                    }
                    userEditor.LoadUserData(((TextureData)SelectedSection).Texture.UserData.ToList());
                }

                editor.Text = Text;
                if (Runtime.DisplayViewport)
                    editor.SetEditorOrientation(true);

                editor.LoadProperties(((TextureData)SelectedSection).Texture);
                editor.LoadImage((TextureData)SelectedSection);
                return;
            }

            if (SelectedSection is BNTX)
            {
                STPropertyGrid editor = (STPropertyGrid)bfresEditor.GetActiveEditor(typeof(STPropertyGrid));
                if (editor == null)
                {
                    editor = new STPropertyGrid();
                    editor.Dock = DockStyle.Fill;
                    bfresEditor.LoadEditor(editor);
                }
                editor.LoadProperty(((BNTX)SelectedSection).BinaryTexFile, OnPropertyChanged);
                return;
            }

            if (SelectedSection is ExternalFileData)
            {
                HexEditor editor = (HexEditor)bfresEditor.GetActiveEditor(typeof(HexEditor));
                if (editor == null)
                {
                    editor = new HexEditor();
                    editor.Dock = DockStyle.Fill;
                    bfresEditor.LoadEditor(editor);
                }
                editor.Text = Text;
                editor.LoadData(((ExternalFileData)SelectedSection).Data);
                return;
            }

            bool IsSimpleEditor = PluginRuntime.UseSimpleBfresEditor;

            if (IsSimpleEditor)
            {
                if (SelectedSection is MatTextureWrapper)
                {
                    SamplerEditorSimple editorT = (SamplerEditorSimple)bfresEditor.GetActiveEditor(typeof(SamplerEditorSimple));
                    if (editorT == null)
                    {
                        editorT = new SamplerEditorSimple();
                        editorT.Dock = DockStyle.Fill;
                        bfresEditor.LoadEditor(editorT);
                    }
                    editorT.Text = Text;
                    editorT.LoadTexture(((MatTextureWrapper)SelectedSection).textureMap);
                    return;
                }
           

                STPropertyGrid editor = (STPropertyGrid)bfresEditor.GetActiveEditor(typeof(STPropertyGrid));
                if (editor == null)
                {
                    editor = new STPropertyGrid();
                    editor.Dock = DockStyle.Fill;
                    bfresEditor.LoadEditor(editor);
                }
                editor.Text = Text;

                if (SelectedSection is BFRES)
                {
                    if (resFile != null)
                        editor.LoadProperty(resFile, OnPropertyChanged);
                    else
                        editor.LoadProperty(resFileU, OnPropertyChanged);
                }
                else if (SelectedSection is FMDL)
                {
                    if (((FMDL)SelectedSection).ModelU != null)
                        editor.LoadProperty(((FMDL)SelectedSection).ModelU, OnPropertyChanged);
                    else
                        editor.LoadProperty(((FMDL)SelectedSection).Model, OnPropertyChanged);
                }
                else if (SelectedSection is FSHP)
                {
                    if (((FSHP)SelectedSection).ShapeU != null)
                        editor.LoadProperty(((FSHP)SelectedSection).ShapeU, OnPropertyChanged);
                    else
                        editor.LoadProperty(((FSHP)SelectedSection).Shape, OnPropertyChanged);
                }
                else if (SelectedSection is FMAT)
                {
                    if (((FMAT)SelectedSection).MaterialU != null)
                        editor.LoadProperty(((FMAT)SelectedSection).MaterialU, OnPropertyChanged);
                    else
                        editor.LoadProperty(((FMAT)SelectedSection).Material, OnPropertyChanged);
                }
                else if (SelectedSection is BfresBone)
                {
                    if (((BfresBone)SelectedSection).BoneU != null)
                        editor.LoadProperty(((BfresBone)SelectedSection).BoneU, OnPropertyChanged);
                    else
                        editor.LoadProperty(((BfresBone)SelectedSection).Bone, OnPropertyChanged);
                }
                else if (SelectedSection is FSKL.fsklNode)
                {
                    if (((FSKL.fsklNode)SelectedSection).SkeletonU != null)
                        editor.LoadProperty(((FSKL.fsklNode)SelectedSection).SkeletonU, OnPropertyChanged);
                    else
                        editor.LoadProperty(((FSKL.fsklNode)SelectedSection).Skeleton, OnPropertyChanged);
                }
                else if (SelectedSection is FSKA)
                {
                    if (((FSKA)SelectedSection).SkeletalAnimU != null)
                        editor.LoadProperty(((FSKA)SelectedSection).SkeletalAnimU, OnPropertyChanged);
                    else
                        editor.LoadProperty(((FSKA)SelectedSection).SkeletalAnim, OnPropertyChanged);
                }
                else if (SelectedSection is FMAA)
                {
                    editor.LoadProperty(((FMAA)SelectedSection).MaterialAnim, OnPropertyChanged);
                }
                else
                    editor.LoadProperty(null, OnPropertyChanged);
            }
            else
            {
                var toolstrips = new List<ToolStripMenuItem>();
                var menu = new ToolStripMenuItem("Animation Loader", null, AnimLoader);

                toolstrips.Add(menu);

                bool IsLoaded = drawables.Count != 0;

                bfresEditor.IsLoaded = IsLoaded;

                if (drawables.Count <= 0)
                {
                    //Add drawables
                    drawables.Add(BFRESRender);

                    for (int m = 0; m < BFRESRender.models.Count; m++)
                        drawables.Add(BFRESRender.models[m].Skeleton);
                }

                if (Runtime.UseViewport)
                    bfresEditor.LoadViewport(drawables, toolstrips);

                if (!IsLoaded)
                {
                    bfresEditor.OnLoadedTab();
                }

                if (SelectedSection is BFRES)
                {
                    STPropertyGrid editor = (STPropertyGrid)bfresEditor.GetActiveEditor(typeof(STPropertyGrid));
                    if (editor == null)
                    {
                        editor = new STPropertyGrid();
                        editor.Dock = DockStyle.Fill;
                        bfresEditor.LoadEditor(editor);
                    }
                    editor.Text = Text;

                    if (resFile != null)
                        editor.LoadProperty(resFile, OnPropertyChanged);
                    else
                        editor.LoadProperty(resFileU, OnPropertyChanged);
                }
                else if (SelectedSection is BFRESGroupNode)
                {
                    STPropertyGrid editor = (STPropertyGrid)bfresEditor.GetActiveEditor(typeof(STPropertyGrid));
                    if (editor == null)
                    {
                        editor = new STPropertyGrid();
                        editor.Dock = DockStyle.Fill;
                        bfresEditor.LoadEditor(editor);
                    }
                    editor.Text = Text;
                    editor.LoadProperty(null, null);
                }
                else if (SelectedSection is FSKL.fsklNode)
                {
                    FSKLEditor editor = (FSKLEditor)bfresEditor.GetActiveEditor(typeof(FSKLEditor));
                    if (editor == null)
                    {
                        editor = new FSKLEditor();
                        editor.Text = Text;
                        editor.Dock = DockStyle.Fill;
                        bfresEditor.LoadEditor(editor);
                    }
                    editor.LoadSkeleton(((FSKL.fsklNode)SelectedSection).fskl);
                }
                else if (SelectedSection is BfresBone)
                {
                    BfresBoneEditor editor = (BfresBoneEditor)bfresEditor.GetActiveEditor(typeof(BfresBoneEditor));
                    if (editor == null)
                    {
                        editor = new BfresBoneEditor();
                        editor.Text = Text;
                        editor.Dock = DockStyle.Fill;
                        bfresEditor.LoadEditor(editor);
                    }
                    editor.LoadBone((BfresBone)SelectedSection);
                }
                else if (SelectedSection is FSHP)
                {
                    BfresShapeEditor editor = (BfresShapeEditor)bfresEditor.GetActiveEditor(typeof(BfresShapeEditor));
                    if (editor == null)
                    {
                        editor = new BfresShapeEditor();
                        editor.Text = Text;
                        editor.Dock = DockStyle.Fill;
                        bfresEditor.LoadEditor(editor);
                    }
                    editor.LoadShape((FSHP)SelectedSection);
                }
                else if (SelectedSection is FMAT)
                {
                    FMATEditor editor = (FMATEditor)bfresEditor.GetActiveEditor(typeof(FMATEditor));
                    if (editor == null)
                    {
                        editor = new FMATEditor();
                        editor.Text = Text;
                        editor.Dock = DockStyle.Fill;
                        bfresEditor.LoadEditor(editor);
                    }
                    editor.LoadMaterial((FMAT)SelectedSection);
                }
                else if (SelectedSection is FSKA.BoneAnimNode)
                {
                    BoneAnimEditor editor = (BoneAnimEditor)bfresEditor.GetActiveEditor(typeof(BoneAnimEditor));
                    if (editor == null)
                    {
                        editor = new BoneAnimEditor();
                        editor.Text = Text;
                        editor.Dock = DockStyle.Fill;
                        bfresEditor.LoadEditor(editor);
                    }
                    editor.LoadBoneAnim((FSKA.BoneAnimNode)SelectedSection);
                }
                else if (SelectedSection is FSCN.BfresCameraAnim)
                {
                    SceneAnimEditor editor = (SceneAnimEditor)bfresEditor.GetActiveEditor(typeof(SceneAnimEditor));
                    if (editor == null)
                    {
                        editor = new SceneAnimEditor();
                        editor.Text = Text;
                        editor.Dock = DockStyle.Fill;
                        bfresEditor.LoadEditor(editor);
                    }
                    editor.LoadCameraAnim((FSCN.BfresCameraAnim)SelectedSection);
                }
                else if (SelectedSection is FSCN.BfresLightAnim)
                {
                    SceneAnimEditor editor = (SceneAnimEditor)bfresEditor.GetActiveEditor(typeof(SceneAnimEditor));
                    if (editor == null)
                    {
                        editor = new SceneAnimEditor();
                        editor.Text = Text;
                        editor.Dock = DockStyle.Fill;
                        bfresEditor.LoadEditor(editor);
                    }
                    editor.LoadLightAnim((FSCN.BfresLightAnim)SelectedSection);
                }
                else if (SelectedSection is FSCN.BfresFogAnim)
                {
                    SceneAnimEditor editor = (SceneAnimEditor)bfresEditor.GetActiveEditor(typeof(SceneAnimEditor));
                    if (editor == null)
                    {
                        editor = new SceneAnimEditor();
                        editor.Text = Text;
                        editor.Dock = DockStyle.Fill;
                        bfresEditor.LoadEditor(editor);
                    }
                    editor.LoadFogAnim((FSCN.BfresFogAnim)SelectedSection);
                }
                else if (SelectedSection is FMDL)
                    OpenSubFileEditor<FMDL>(SelectedSection, bfresEditor);
                else if (SelectedSection is FSKA)
                    OpenSubFileEditor<FSKA>(SelectedSection, bfresEditor);
                else if (SelectedSection is FSHU)
                    OpenSubFileEditor<FSHU>(SelectedSection, bfresEditor);
                else if (SelectedSection is FSCN)
                    OpenSubFileEditor<FSCN>(SelectedSection, bfresEditor);
                else if (SelectedSection is FSHA)
                    OpenSubFileEditor<FSHA>(SelectedSection, bfresEditor);
                else if (SelectedSection is FTXP)
                    OpenSubFileEditor<FTXP>(SelectedSection, bfresEditor);
                else if (SelectedSection is FMAA)
                    OpenSubFileEditor<FMAA>(SelectedSection, bfresEditor);
                else if (SelectedSection is FVIS)
                    OpenSubFileEditor<FVIS>(SelectedSection, bfresEditor);


                /*   else if (SelectedSection is FMAA && ((FMAA)SelectedSection).AnimType == MaterialAnimation.AnimationType.TexturePattern)
                {
                    BfresTexturePatternEditor editor = (BfresTexturePatternEditor)bfresEditor.GetActiveEditor(typeof(BfresTexturePatternEditor));
                    if (editor == null)
                    {
                        editor = new BfresTexturePatternEditor();
                        bfresEditor.LoadEditor(editor);
                    }
                    editor.Text = Text;
                    editor.Dock = DockStyle.Fill;
                    editor.LoadAnim((FMAA)SelectedSection);
                }*/
            }
        }

        private SubFileEditor OpenSubFileEditor<T>(object node, BfresEditor bfresEditor) where T : STGenericWrapper
        {
            SubFileEditor editor = (SubFileEditor)bfresEditor.GetActiveEditor(typeof(SubFileEditor));
            if (editor == null)
            {
                editor = new SubFileEditor();
                editor.Dock = DockStyle.Fill;
                bfresEditor.LoadEditor(editor);
            }
            editor.LoadSubFile<T>(node);

            return editor;
        }

        public void AnimLoader(object sender, EventArgs args)
        {
            AnimationLoader loader = new AnimationLoader();
            loader.TopLevel = true;
            loader.Show();
        }

        public BFRESRender BFRESRender;
        public void Load(System.IO.Stream stream)
        {
            CanSave = true;

            ImageKey = "bfres";
            SelectedImageKey = "bfres";

            using (FileReader reader = new FileReader(stream, true))
            {
                reader.ByteOrder = Syroot.BinaryData.ByteOrder.BigEndian;
                reader.Position = 4;

                if (reader.ReadInt32() != 0x20202020)
                    IsWiiU = true;

                reader.Position = 0;
            }

            LoadMenus(IsWiiU);

       
            BFRESRender = new BFRESRender();
            BFRESRender.ModelTransform = MarioCostumeEditor.SetTransform(FileName);
            BFRESRender.ResFileNode = this;


            if (IsWiiU)
            {
                LoadFile(new Syroot.NintenTools.Bfres.ResFile(stream));
            }
            else
            {
                LoadFile(new Syroot.NintenTools.NSW.Bfres.ResFile(stream));
            }
        }
        public void Unload()
        {
            BFRESRender.Destroy();

            foreach (var node in TreeViewExtensions.Collect(BFRESRender.ResFileNode.Nodes))
            {
                if (node is BFRESGroupNode)
                {
                    if (((BFRESGroupNode)node).Type == BRESGroupType.Textures)
                    {
                        if (PluginRuntime.ftexContainers.Contains(((BFRESGroupNode)node)))
                            PluginRuntime.ftexContainers.Remove(((BFRESGroupNode)node));
                    }
                }

                if (node is BNTX)
                    ((BNTX)node).Unload();
            }
            Nodes.Clear();
        }

        public byte[] Save()
        {
            MemoryStream mem = new MemoryStream();

            if (IsWiiU)
                SaveWiiU(mem);
            else
                SaveSwitch(mem);

            return mem.ToArray();
        }

        public ResFile resFile = null;
        public ResU.ResFile resFileU = null;

        public override void OnClick(TreeView treeView)
        {
            LoadEditors(this);
        }

        public void LoadFile(ResU.ResFile res)
        {
            CanDelete = true;

            resFileU = res;

            Text = resFileU.Name;

            var modelFolder = new BFRESGroupNode(BRESGroupType.Models);
            var texturesFolder = new BFRESGroupNode(BRESGroupType.Textures);
            var animFolder = new BFRESAnimFolder();
            var externalFilesFolder = new BFRESGroupNode(BRESGroupType.Embedded);

            Nodes.Add(modelFolder);
            Nodes.Add(texturesFolder);
            Nodes.Add(animFolder);
            Nodes.Add(externalFilesFolder);

            animFolder.LoadMenus(IsWiiU);
            PluginRuntime.ftexContainers.Add(texturesFolder);

            if (resFileU.Models.Count > 0)
            {
                for (int i = 0; i < resFileU.Models.Count; i++)
                {
                    var fmdl = new FMDL();
                    BfresWiiU.ReadModel(fmdl, resFileU.Models[i]);
                    modelFolder.AddNode(fmdl);
                }
            }
            if (resFileU.Textures.Count > 0)
            {
                for (int i = 0; i < resFileU.Textures.Count; i++)
                {
                    var ftex = new FTEX(resFileU.Textures[i]);
                    texturesFolder.AddNode(ftex);
                    ftex.UpdateMipMaps();
                }
            }
            if (resFileU.SkeletalAnims.Count > 0)
            {
                var group = new BFRESGroupNode(BRESGroupType.SkeletalAnim);
                animFolder.Nodes.Add(group);

                for (int i = 0; i < resFileU.SkeletalAnims.Count; i++)
                    group.AddNode(new FSKA(resFileU.SkeletalAnims[i]));
            }
            if (resFileU.ShaderParamAnims.Count > 0)
            {
                var group = new BFRESGroupNode(BRESGroupType.ShaderParamAnim);
                animFolder.Nodes.Add(group);

                for (int i = 0; i < resFileU.ShaderParamAnims.Count; i++)
                    group.AddNode(new FSHU(resFileU.ShaderParamAnims[i], MaterialAnimation.AnimationType.ShaderParam));
            }
            if (resFileU.ColorAnims.Count > 0)
            {
                var group = new BFRESGroupNode(BRESGroupType.ColorAnim);
                animFolder.Nodes.Add(group);

                for (int i = 0; i < resFileU.ColorAnims.Count; i++)
                    group.AddNode(new FSHU(resFileU.ColorAnims[i], MaterialAnimation.AnimationType.Color));
            }
            if (resFileU.TexSrtAnims.Count > 0)
            {
                var group = new BFRESGroupNode(BRESGroupType.TexSrtAnim);
                animFolder.Nodes.Add(group);

                for (int i = 0; i < resFileU.TexSrtAnims.Count; i++)
                    group.AddNode(new FSHU(resFileU.TexSrtAnims[i], MaterialAnimation.AnimationType.TexturePattern));
            }
            if (resFileU.TexPatternAnims.Count > 0)
            {
                var group = new BFRESGroupNode(BRESGroupType.TexPatAnim);
                animFolder.Nodes.Add(group);

                for (int i = 0; i < resFileU.TexPatternAnims.Count; i++)
                    group.AddNode(new FTXP(resFileU.TexPatternAnims[i]));
            }
            if (resFileU.ShapeAnims.Count > 0)
            {
                var group = new BFRESGroupNode(BRESGroupType.ShapeAnim);
                animFolder.Nodes.Add(group);

                for (int i = 0; i < resFileU.ShapeAnims.Count; i++)
                    group.AddNode(new FSHA(resFileU.ShapeAnims[i]));
            }
            if (resFileU.BoneVisibilityAnims.Count > 0)
            {
                var group = new BFRESGroupNode(BRESGroupType.BoneVisAnim);
                animFolder.Nodes.Add(group);

                for (int i = 0; i < resFileU.BoneVisibilityAnims.Count; i++)
                    group.AddNode(new FVIS(resFileU.BoneVisibilityAnims[i]));
            }
            if (resFileU.MatVisibilityAnims.Count > 0)
            {
                var group = new BFRESGroupNode(BRESGroupType.MatVisAnim);
                animFolder.Nodes.Add(group);

                for (int i = 0; i < resFileU.MatVisibilityAnims.Count; i++)
                    group.AddNode(new FVIS(resFileU.MatVisibilityAnims[i]));
            }
            if (resFileU.SceneAnims.Count > 0)
            {
                var group = new BFRESGroupNode(BRESGroupType.SceneAnim);
                animFolder.Nodes.Add(group);

                for (int i = 0; i < resFileU.SceneAnims.Count; i++)
                    group.AddNode(new FSCN(resFileU.SceneAnims[i]));
            }
            if (resFileU.ExternalFiles.Count > 0)
            {
                foreach (var anim in resFileU.ExternalFiles)
                {
                    externalFilesFolder.AddNode(new ExternalFileData(anim.Key, anim.Value.Data));
                }
            }

            if (PluginRuntime.UseSimpleBfresEditor)
                LoadSimpleMode();
        }
        public void LoadFile(ResFile res)
        {
            resFile = res;

            Text = resFile.Name;

            var modelFolder = new BFRESGroupNode(BRESGroupType.Models);
            var texturesFolder = new BNTX() { ImageKey = "folder", SelectedImageKey = "folder", Text = "Textures" };
            var animFolder = new BFRESAnimFolder();
            var externalFilesFolder = new BFRESGroupNode(BRESGroupType.Embedded);

            //Texture folder acts like a bntx for saving back
            //This will only save if the user adds textures to it or the file has a bntx already
            texturesFolder.IFileInfo = new IFileInfo();
            texturesFolder.FileName = "Textures";
            texturesFolder.Load(new MemoryStream(BNTX.CreateNewBNTX("Textures")));

            animFolder.LoadMenus(IsWiiU);

            Nodes.Add(modelFolder);
            Nodes.Add(texturesFolder);
            Nodes.Add(animFolder);
            Nodes.Add(externalFilesFolder);

            if (resFile.Models.Count > 0)
            {
                for (int i = 0; i < resFile.Models.Count; i++)
                {
                    var fmdl = new FMDL();
                    BfresSwitch.ReadModel(fmdl, resFile.Models[i]);
                    modelFolder.AddNode(fmdl);
                }
            }
            if (resFile.SkeletalAnims.Count > 0)
            {
                var group = new BFRESGroupNode(BRESGroupType.SkeletalAnim);
                animFolder.Nodes.Add(group);

                for (int i = 0; i < resFile.SkeletalAnims.Count; i++)
                    group.AddNode(new FSKA(resFile.SkeletalAnims[i]));
            }
            if (resFile.MaterialAnims.Count > 0)
            {
                var group = new BFRESGroupNode(BRESGroupType.ShaderParamAnim);
                var group2 = new BFRESGroupNode(BRESGroupType.TexSrtAnim);
                var group3 = new BFRESGroupNode(BRESGroupType.TexPatAnim);
                var group4 = new BFRESGroupNode(BRESGroupType.ColorAnim);
                var group5 = new BFRESGroupNode(BRESGroupType.MatVisAnim);
                var group6 = new BFRESGroupNode(BRESGroupType.MaterialAnim);

                bool HasShaderParamsAnim = false;
                bool HasTextureSrtAnim = false;
                bool HasTexturePatternAnim = false;
                bool HasColorAnim = false;
                bool HasMatVisAnim = false;
                bool HasMaterialAnim = false;

                for (int i = 0; i < resFile.MaterialAnims.Count; i++)
                {
                    var anim = resFile.MaterialAnims[i];
                    if (FMAA.IsShaderParamAnimation(anim.Name))
                    {
                        group.AddNode(new FMAA(anim, MaterialAnimation.AnimationType.ShaderParam));
                        HasShaderParamsAnim = true;
                    }
                    else if (FMAA.IsSRTAnimation(anim.Name))
                    {
                        group2.AddNode(new FMAA(anim, MaterialAnimation.AnimationType.TextureSrt));
                        HasTextureSrtAnim = true;
                    }
                    else if(FMAA.IsTexturePattern(anim.Name))
                    {
                        group3.AddNode(new FMAA(anim, MaterialAnimation.AnimationType.TexturePattern));
                        HasTexturePatternAnim = true;
                    }
                    else if (FMAA.IsColorAnimation(anim.Name))
                    {
                        group4.AddNode(new FMAA(anim, MaterialAnimation.AnimationType.Color));
                        HasColorAnim = true;
                    }
                    else if (FMAA.IsVisibiltyAnimation(anim.Name))
                    {
                        group5.AddNode(new FMAA(anim, MaterialAnimation.AnimationType.Visibilty));
                        HasMatVisAnim = true;
                    }
                    else
                    {
                        group6.AddNode(new FMAA(anim, MaterialAnimation.AnimationType.ShaderParam));
                        HasMaterialAnim = true;
                    }
                }

                if (HasShaderParamsAnim)
                    animFolder.Nodes.Add(group);
                if (HasTextureSrtAnim)
                    animFolder.Nodes.Add(group2);
                if (HasTexturePatternAnim)
                    animFolder.Nodes.Add(group3);
                if (HasColorAnim)
                    animFolder.Nodes.Add(group4);
                if (HasMatVisAnim)
                    animFolder.Nodes.Add(group5);
                if (HasMaterialAnim)
                    animFolder.Nodes.Add(group6);
            }
            if (resFile.BoneVisibilityAnims.Count > 0)
            {
                var group = new BFRESGroupNode(BRESGroupType.BoneVisAnim);
                animFolder.Nodes.Add(group);

                for (int i = 0; i < resFile.BoneVisibilityAnims.Count; i++)
                    group.AddNode(new FVIS(resFile.BoneVisibilityAnims[i]));
            }
            if (resFile.SceneAnims.Count > 0)
            {
                var group = new BFRESGroupNode(BRESGroupType.SceneAnim);
                animFolder.Nodes.Add(group);

                for (int i = 0; i < resFile.SceneAnims.Count; i++)
                    group.AddNode(new FSCN(resFile.SceneAnims[i]));
            }
            if (resFile.ShapeAnims.Count > 0)
            {
                var group = new BFRESGroupNode(BRESGroupType.ShapeAnim);
                animFolder.Nodes.Add(group);

                for (int i = 0; i < resFile.ShapeAnims.Count; i++)
                    group.AddNode(new FSHA(resFile.ShapeAnims[i]));
            }
            if (resFile.ExternalFiles.Count > 0)
            {
                int index = 0;

                //This also can be set to true if we don't want to rebuild the bntx for debug purposes
                bool IsTexturesReplaced = false;

                foreach (var anim in resFile.ExternalFiles)
                {
                    // group.AddNode(new ExternalFileData(Name, anim.Data) { FileFormat = file });

                    string Name = resFile.ExternalFileDict.GetKey(index++);

                    var file = STFileLoader.OpenFileFormat(Name, anim.Data, false, true);

                    //Only do once. There's usually one bntx embedded but incase there are multiple
                    if (file is BNTX && !IsTexturesReplaced)
                    {
                        ((TreeNode)file).Text = texturesFolder.Text;
                        ((TreeNode)file).ImageKey = texturesFolder.ImageKey;
                        ((TreeNode)file).SelectedImageKey = texturesFolder.SelectedImageKey;

                        //Remove temporary bntx file with file one
                        PluginRuntime.bntxContainers.Remove(texturesFolder);

                        ReplaceNode(texturesFolder.Parent, texturesFolder, (TreeNode)file);

                        IsTexturesReplaced = true;
                    }
                    else
                    {
                        externalFilesFolder.AddNode(new ExternalFileData(Name, anim.Data));
                    }
                }
            }

            if (PluginRuntime.UseSimpleBfresEditor)
                LoadSimpleMode();
        }

        public static void ReplaceNode(TreeNode node, TreeNode replaceNode, TreeNode NewNode)
        {
            if (NewNode == null)
                return;

            int index = node.Nodes.IndexOf(replaceNode);
            node.Nodes.RemoveAt(index);
            node.Nodes.Insert(index, NewNode);
        }

        public override void Export(string FileName)
        {
            bool IsTex1 = FileName.Contains("Tex1");
            bool HasTextures = false;

            foreach (TreeNode group in Nodes)
            {
                if (group is BFRESGroupNode)
                {
                    if (((BFRESGroupNode)group).Type == BRESGroupType.Textures && group.Nodes.Count > 0)
                        HasTextures = true;
                }
            }
 
            if (IsTex1 && HasTextures)
            {
                STFileSaver.SaveFileFormat(this, FileName);

                byte[] Tex2 = GenerateTex2();

                SaveFileDialog sfd = new SaveFileDialog();
                sfd.FileName = FileName.Replace("Tex1", "Tex2");
                sfd.DefaultExt = ".sbfres";

                List<IFileFormat> formats = new List<IFileFormat>();
                formats.Add(this);
                sfd.Filter = Utils.GetAllFilters(formats);

                if (sfd.ShowDialog() == DialogResult.OK)
                    STFileSaver.SaveFileFormat(Tex2, true, 0, CompressionType.Yaz0, sfd.FileName);
            }
            else
                STFileSaver.SaveFileFormat(this, FileName);
        }

        private byte[] GenerateTex2()
        {
            var mem = new MemoryStream();

            var resFileU = BFRESRender.ResFileNode.resFileU;

            //Create a tex2 file
            ResU.ResFile resFileTex2 = new ResU.ResFile();
            resFileTex2.Alignment = resFileU.Alignment;
            resFileTex2.Name = resFileU.Name.Replace("Tex1", "Tex2");
            resFileTex2.VersionMajor = resFileU.VersionMajor;
            resFileTex2.VersionMajor2 = resFileU.VersionMajor2;
            resFileTex2.VersionMinor = resFileU.VersionMinor;
            resFileTex2.VersionMinor2 = resFileU.VersionMinor2;
            resFileTex2.Textures = resFileU.Textures;

            foreach (var group in Nodes)
            {
                if (group is BFRESGroupNode)
                {
                    if (((BFRESGroupNode)group).Type != BRESGroupType.Textures)
                        continue;

                    foreach (FTEX tex in ((BFRESGroupNode)group).Nodes)
                    {
                        if (resFileTex2.Textures.ContainsKey(tex.Text))
                        {
                            resFileTex2.Textures[tex.Text].MipData = tex.texture.MipData;
                            resFileTex2.Textures[tex.Text].MipOffsets = tex.texture.MipOffsets;
                            resFileTex2.Textures[tex.Text].MipCount = tex.texture.MipCount;
                            resFileTex2.Textures[tex.Text].Swizzle = tex.Tex2Swizzle;
                        }
                    }
                }
            }

            resFileTex2.Save(mem);

            return mem.ToArray();
        }

        private void SaveTex2(string fileName)
        {
            bool Compressed = fileName.EndsWith("sbfres");

            byte[] data;
            if (Compressed)
                data = EveryFileExplorer.YAZ0.Decompress(fileName);
            else
                data = File.ReadAllBytes(fileName);

            ResU.ResFile resFileTex2 = new ResU.ResFile(new MemoryStream(data));

        
            foreach (BFRESGroupNode group in Nodes)
            {
                if (group.Type != BRESGroupType.Textures)
                    return;

                foreach (FTEX tex in group.Nodes)
                {
                    if (resFileTex2.Textures.ContainsKey(tex.Text))
                    {
                        resFileTex2.Textures[tex.Text].MipData = tex.texture.MipData;
                        resFileTex2.Textures[tex.Text].MipOffsets = tex.texture.MipOffsets;
                        resFileTex2.Textures[tex.Text].MipCount = tex.texture.MipCount;
                    }
                }
            }
            MemoryStream mem2 = new MemoryStream();
            resFileTex2.Save(mem2);

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = FileName + "NewTex2.sbfres";

            List<IFileFormat> formats = new List<IFileFormat>();
            formats.Add(this);
            sfd.Filter = Utils.GetAllFilters(formats);

            if (sfd.ShowDialog() == DialogResult.OK)
                STFileSaver.SaveFileFormat(mem2.ToArray(), Compressed, 0, CompressionType.Yaz0, sfd.FileName);
        }
        private void Rename(object sender, EventArgs args)
        {
            RenameDialog dialog = new RenameDialog();
            dialog.SetString(Text);

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                Text = dialog.textBox1.Text;
            }
        }
        private void Remove(object sender, EventArgs args)
        {
            Unload();
        }
        private void SaveSwitch(MemoryStream mem)
        {
            var resFile = BFRESRender.ResFileNode.resFile;

            resFile.Models.Clear();
            resFile.SkeletalAnims.Clear();
            resFile.MaterialAnims.Clear();
            resFile.SceneAnims.Clear();
            resFile.ShapeAnims.Clear();
            resFile.BoneVisibilityAnims.Clear();
            resFile.ModelDict.Clear();
            resFile.SkeletalAnimDict.Clear();
            resFile.MaterialAnimDict.Clear();
            resFile.SceneAnimDict.Clear();
            resFile.ShapeAnimDict.Clear();
            resFile.BoneVisibilityAnimDict.Clear();
            resFile.ExternalFiles.Clear();
            resFile.ExternalFileDict.Clear();

            foreach (TreeNode node in Nodes)
            {
                if (node is BFRESGroupNode)
                    SaveBfresSwitchGroup((BFRESGroupNode)node, resFile);
                
                if (node is BFRESAnimFolder)
                {
                    foreach (var animGroup in node.Nodes)
                        SaveBfresSwitchGroup((BFRESGroupNode)animGroup, resFile);
                }
                if (node is BNTX)
                {
                    if (((BNTX)node).Textures.Count > 0)
                    {
                        resFile.ExternalFiles.Add(new ExternalFile() { Data = ((BNTX)node).Save() });
                        resFile.ExternalFileDict.Add("textures.bntx");
                    }
                }
            }

            ErrorCheck();
            resFile.Save(mem);
        }

        private static void SaveBfresSwitchGroup(BFRESGroupNode group, ResFile resFile)
        {
            switch (group.Type)
            {
                case BRESGroupType.Models:
                    foreach (FMDL model in group.Nodes)
                    {
                        model.Model.Name = model.Text;
                        resFile.Models.Add(BfresSwitch.SetModel(model));
                    }
                    break;
                case BRESGroupType.SkeletalAnim:
                    foreach (FSKA ska in group.Nodes)
                    {
                      //  ska.SkeletalAnim.BoneAnims.Clear();

                      //  foreach (FSKA.BoneAnimNode bone in ska.Bones)
                          //  ska.SkeletalAnim.BoneAnims.Add(bone.SaveData());

                        ska.SkeletalAnim.Name = ska.Text;
                        resFile.SkeletalAnims.Add(ska.SkeletalAnim);
                    }
                    break;
                case BRESGroupType.TexPatAnim:
                case BRESGroupType.ShaderParamAnim:
                case BRESGroupType.ColorAnim:
                case BRESGroupType.TexSrtAnim:
                case BRESGroupType.MatVisAnim:
                    foreach (FMAA fmaa in group.Nodes)
                    {
                        fmaa.MaterialAnim.MaterialAnimDataList.Clear();
                        foreach (FMAA.MaterialAnimEntry mat in fmaa.Materials)
                            fmaa.MaterialAnim.MaterialAnimDataList.Add(mat.SaveData());

                        fmaa.SetName();
                        fmaa.SaveAnimData();
                        resFile.MaterialAnims.Add(fmaa.MaterialAnim);
                    }
                    break;
                case BRESGroupType.BoneVisAnim:
                    foreach (FVIS fbnv in group.Nodes)
                    {
                        fbnv.SaveData();
                        fbnv.VisibilityAnim.Name = fbnv.Text;
                        resFile.BoneVisibilityAnims.Add(fbnv.VisibilityAnim);
                    }
                    break;
                case BRESGroupType.ShapeAnim:
                    foreach (FSHA fsha in group.Nodes)
                    {
                        fsha.ShapeAnim.VertexShapeAnims.Clear();
                        foreach (FSHA.ShapeAnimEntry shp in fsha.Nodes)
                            fsha.ShapeAnim.VertexShapeAnims.Add(shp.SaveData());

                        fsha.ShapeAnim.Name = fsha.Text;
                        resFile.ShapeAnims.Add(fsha.ShapeAnim);
                    }
                    break;
                case BRESGroupType.SceneAnim:
                    foreach (FSCN fscn in group.Nodes)
                    {
                        fscn.SceneAnim.Name = fscn.Text;
                        resFile.SceneAnims.Add(fscn.SceneAnim);
                    }
                    break;
                case BRESGroupType.Embedded:
                    foreach (var ext in group.Nodes)
                    {
                        if (ext is ExternalFileData)
                        {
                            resFile.ExternalFiles.Add(new ExternalFile() { Data = ((ExternalFileData)ext).Data });
                            resFile.ExternalFileDict.Add(((ExternalFileData)ext).Text);
                        }
                        else if (ext is TreeNodeFile)
                        {
                            resFile.ExternalFiles.Add(new ExternalFile() { Data = ((IFileFormat)ext).Save() });
                            resFile.ExternalFileDict.Add(((TreeNodeFile)ext).Text);
                        }

                    }
                    break;
            }
        }

        private void SaveMaterialAnims(TreeNodeCollection nodes)
        {

        }
            

        private void SaveWiiU(MemoryStream mem)
        {
            var resFileU = BFRESRender.ResFileNode.resFileU;

            resFileU.Models.Clear();
            resFileU.Textures.Clear();
            resFileU.SkeletalAnims.Clear();
            resFileU.ShaderParamAnims.Clear();
            resFileU.ColorAnims.Clear();
            resFileU.TexSrtAnims.Clear();
            resFileU.TexPatternAnims.Clear();
            resFileU.BoneVisibilityAnims.Clear();
            resFileU.MatVisibilityAnims.Clear();
            resFileU.ShapeAnims.Clear();
            resFileU.SceneAnims.Clear();
            resFileU.ExternalFiles.Clear();

            bool IsTex1 = FilePath.Contains("Tex1");

            foreach (TreeNode node in Nodes)
            {
                if (node is BFRESGroupNode)
                    SaveBfresWiiUGroup((BFRESGroupNode)node, resFileU);

                if (node is BFRESAnimFolder)
                {
                    foreach (var animGroup in node.Nodes)
                        SaveBfresWiiUGroup((BFRESGroupNode)animGroup, resFileU);
                }
            }

            //     ErrorCheck();
            resFileU.Save(mem);
        }

        private static void SaveBfresWiiUGroup(BFRESGroupNode group, ResU.ResFile resFileU)
        {
            switch (group.Type)
            {
                case BRESGroupType.Models:
                    foreach (FMDL model in group.Nodes)
                    {
                        model.ModelU.Name = model.Text;
                        resFileU.Models.Add(model.Text, BfresWiiU.SetModel(model));
                    }
                    break;
                case BRESGroupType.Textures:
                    foreach (FTEX tex in group.Nodes)
                    {
                        tex.texture.Name = tex.Text;
                        resFileU.Textures.Add(tex.Text, tex.texture);
                    }
                    break;
                case BRESGroupType.SkeletalAnim:
                    foreach (FSKA ska in group.Nodes)
                    {
                        ska.SkeletalAnimU.BoneAnims.Clear();
                        foreach (FSKA.BoneAnimNode bone in ska.Bones)
                            ska.SkeletalAnimU.BoneAnims.Add(bone.SaveDataU());

                        ska.SkeletalAnimU.Name = ska.Text;
                        resFileU.SkeletalAnims.Add(ska.Text, ska.SkeletalAnimU);
                    }
                    break;
                case BRESGroupType.ShaderParamAnim:
                    foreach (FSHU anim in group.Nodes)
                    {
                        anim.ShaderParamAnim.ShaderParamMatAnims.Clear();
                        foreach (FSHU.MaterialAnimEntry bone in anim.Materials)
                            anim.ShaderParamAnim.ShaderParamMatAnims.Add(bone.SaveData());


                        anim.ShaderParamAnim.Name = anim.Text;
                        resFileU.ShaderParamAnims.Add(anim.Text, anim.ShaderParamAnim);
                    }
                    break;
                case BRESGroupType.ColorAnim:
                    foreach (FSHU anim in group.Nodes)
                    {
                        anim.ShaderParamAnim.ShaderParamMatAnims.Clear();
                        foreach (FSHU.MaterialAnimEntry bone in anim.Materials)
                            anim.ShaderParamAnim.ShaderParamMatAnims.Add(bone.SaveData());

                        anim.ShaderParamAnim.Name = anim.Text;
                        resFileU.ColorAnims.Add(anim.Text, anim.ShaderParamAnim);
                    }
                    break;
                case BRESGroupType.TexSrtAnim:
                    foreach (FSHU anim in group.Nodes)
                    {
                        anim.ShaderParamAnim.ShaderParamMatAnims.Clear();
                        foreach (FSHU.MaterialAnimEntry bone in anim.Materials)
                            anim.ShaderParamAnim.ShaderParamMatAnims.Add(bone.SaveData());

                        anim.ShaderParamAnim.Name = anim.Text;
                        resFileU.TexSrtAnims.Add(anim.Text, anim.ShaderParamAnim);
                    }
                    break;
                case BRESGroupType.TexPatAnim:
                    foreach (FTXP anim in group.Nodes)
                    {
                        anim.TexPatternAnim.Name = anim.Text;
                        resFileU.TexPatternAnims.Add(anim.Text, anim.TexPatternAnim);
                    }
                    break;
                case BRESGroupType.MatVisAnim:
                    foreach (FVIS anim in group.Nodes)
                    {
                        anim.SaveData();
                        anim.VisibilityAnimU.Name = anim.Text;
                        resFileU.MatVisibilityAnims.Add(anim.Text, anim.VisibilityAnimU);
                    }
                    break;
                case BRESGroupType.BoneVisAnim:
                    foreach (FVIS anim in group.Nodes)
                    {
                        anim.SaveData();
                        anim.VisibilityAnimU.Name = anim.Text;
                        resFileU.BoneVisibilityAnims.Add(anim.Text, anim.VisibilityAnimU);
                    }
                    break;
                case BRESGroupType.ShapeAnim:
                    foreach (FSHA anim in group.Nodes)
                    {
                        anim.ShapeAnimU.VertexShapeAnims.Clear();
                        foreach (FSHA.ShapeAnimEntry shp in anim.Nodes)
                            anim.ShapeAnimU.VertexShapeAnims.Add(shp.SaveDataU());

                        anim.ShapeAnimU.Name = anim.Text;
                        resFileU.ShapeAnims.Add(anim.Text, anim.ShapeAnimU);
                    }
                    break;
                case BRESGroupType.SceneAnim:
                    foreach (FSCN anim in group.Nodes)
                    {
                        anim.SceneAnimU.Name = anim.Text;
                        resFileU.SceneAnims.Add(anim.Text, anim.SceneAnimU);
                    }
                    break;
                case BRESGroupType.Embedded:
                    foreach (TreeNode ext in group.Nodes)
                    {
                        if (ext is ExternalFileData)
                        {
                            resFileU.ExternalFiles.Add(ext.Text, new ResU.ExternalFile() { Data = ((ExternalFileData)ext).Data });
                        }
                    }
                    break;
            }
        }

        public static void SetShaderAssignAttributes(FMAT.ShaderAssign shd, FSHP shape)
        {
            foreach (var att in shape.vertexAttributes)
            {
                if (!shd.attributes.ContainsValue(att.Name))
                {
                    try
                    {
                        shd.attributes.Add(att.Name, att.Name);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Attribute link failed! \n " + ex);
                    }
                }
            }
            foreach (var tex in shape.GetMaterial().TextureMaps)
            {
                if (!shd.samplers.ContainsValue(tex.SamplerName))
                {
                    try
                    {
                        shd.samplers.Add(tex.SamplerName, tex.SamplerName);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Sampler link failed! \n " + ex);
                    }
                }
            }
        }


        private void SetDuplicateShapeName(FSHP shape)
        {
            DialogResult dialogResult = MessageBox.Show($"A shape {shape.Text} already exists with that name", "", MessageBoxButtons.OK);

            if (dialogResult == DialogResult.OK)
            {
                RenameDialog renameDialog = new RenameDialog();
                renameDialog.Text = "Rename Texture";
                if (renameDialog.ShowDialog() == DialogResult.OK)
                {
                    shape.Text = renameDialog.textBox1.Text;
                }
            }
        }

        static bool ImportMissingTextures = false;
        public static void CheckMissingTextures(FSHP shape)
        {
            // FSHP > Objects > FMDL > Models > BFRES
            BFRES root = (BFRES)shape.Parent.Parent.Parent.Parent;

            foreach (var node in root.Nodes)
            {
                if (node is BNTX)
                {
                    BNTX bntx = (BNTX)node;
                    if (bntx.Textures.Count == 0)
                        return;

                    foreach (MatTexture tex in shape.GetMaterial().TextureMaps)
                    {
                        if (!bntx.Textures.ContainsKey(tex.Name))
                        {
                            if (!ImportMissingTextures)
                            {
                                DialogResult result = MessageBox.Show("Missing textures found! Would you like to use placeholders?", "", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                                if (result == DialogResult.Yes)
                                {
                                    ImportMissingTextures = true;
                                }
                                else
                                    return;
                            }

                            if (ImportMissingTextures)
                                bntx.ImportPlaceholderTexture(tex.Name);
                        }
                    }
                }
            }

            foreach (var node in root.Nodes)
            {
                if (node is BFRESGroupNode && ((BFRESGroupNode)node).Type == BRESGroupType.Textures)
                {
                    if (((BFRESGroupNode)node).ResourceNodes.Count <= 0)
                        return;

                    foreach (MatTexture tex in shape.GetMaterial().TextureMaps)
                    {
                        if (!((BFRESGroupNode)node).ResourceNodes.ContainsKey(tex.Name))
                        {
                            if (!ImportMissingTextures)
                            {
                                DialogResult result = MessageBox.Show("Missing textures found! Would you like to use placeholders?", "", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                                if (result == DialogResult.Yes)
                                {
                                    ImportMissingTextures = true;
                                }
                                else
                                {
                                    return;
                                }
                            }

                            if (ImportMissingTextures)
                                ((BFRESGroupNode)node).ImportPlaceholderTexture(tex.Name);
                        }
                    }
                }
            }
        }

        public void ErrorCheck()
        {
            if (BFRESRender != null)
            {
                List<Errors> Errors = new List<Errors>();
                foreach (FMDL model in BFRESRender.models)
                {
                    foreach (FSHP shp in model.shapes)
                    {
                        if (!IsWiiU)
                        {
                            Syroot.NintenTools.NSW.Bfres.VertexBuffer vtx = shp.VertexBuffer;
                            Syroot.NintenTools.NSW.Bfres.Material mat = shp.GetMaterial().Material;
                            Syroot.NintenTools.NSW.Bfres.ShaderAssign shdr = mat.ShaderAssign;

                            for (int att = 0; att < vtx.Attributes.Count; att++)
                            {
                                if (!shdr.AttribAssigns.Contains(vtx.Attributes[att].Name))
                                    STConsole.WriteLine($"Error! Attribute {vtx.Attributes[att].Name} is unlinked!");
                            }
                            for (int att = 0; att < mat.Samplers.Count; att++)
                            {
                                if (!shdr.SamplerAssigns.Contains(mat.SamplerDict.GetKey(att))) //mat.SamplerDict[att]
                                    STConsole.WriteLine($"Error! Sampler {mat.Samplers[att].Name} is unlinked!");
                            }
                        }
                        else
                        {
                            Syroot.NintenTools.Bfres.VertexBuffer vtx = shp.VertexBufferU;
                            Syroot.NintenTools.Bfres.Material mat = shp.GetMaterial().MaterialU;
                            Syroot.NintenTools.Bfres.ShaderAssign shdr = mat.ShaderAssign;

                            for (int att = 0; att < vtx.Attributes.Count; att++)
                            {
                                 ResU.ResString str = new ResU.ResString();
                                str.String = vtx.Attributes[att].Name;
                                if (!shdr.AttribAssigns.ContainsValue(str))
                                    STConsole.WriteLine($"Error! Attribute {vtx.Attributes[att].Name} is unlinked!");
                            }

                            for (int att = 0; att < mat.Samplers.Count; att++)
                            {
                                ResU.ResString str2 = new ResU.ResString();
                                str2.String = mat.Samplers[att].Name;
                                if (!shdr.SamplerAssigns.ContainsValue(str2)) //mat.SamplerDict[att]
                                    STConsole.WriteLine($"Error! Sampler {mat.Samplers[att].Name} is unlinked!");
                            }
                        }
                    }
                }
             //   ErrorList errorList = new ErrorList();
             //   errorList.LoadList(Errors);
            //    errorList.Show();
            }
        }
        public class Errors
        {
            public string Section = "None";
            public string Section2 = "None";
            public string Message = "";
            public string Type = "Unkown";
        }
    }
}
