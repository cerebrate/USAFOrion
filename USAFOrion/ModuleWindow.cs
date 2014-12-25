using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Tac ;

namespace USAFOrion
{
    public abstract class ModuleWindow<T> : Window<T>
    {
        protected PartModule myPartModule ;

        protected ModuleWindow (string windowTitle, PartModule p, float defaultWidth, float defaultHeight)
            : base (windowTitle, defaultWidth, defaultHeight) { this.myPartModule = p ; }
    }
}
