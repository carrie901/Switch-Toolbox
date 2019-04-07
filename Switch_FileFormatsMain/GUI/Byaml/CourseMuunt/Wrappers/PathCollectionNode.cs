﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Switch_Toolbox.Library;

namespace FirstPlugin.Turbo.CourseMuuntStructs
{
    public class PathCollectionNode : TreeNodeCustom
    {
        public PathCollectionNode(string text) { Text = text; Checked = true; }

        public List<PathGroup> PathGroups = new List<PathGroup>();
    }
}