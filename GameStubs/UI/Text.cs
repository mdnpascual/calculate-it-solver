// NOT GAME CODE. Names and signatures only, so the CalcHook mod compiles on CI without UnityEngine.UI.dll.
// The real chain is Text : MaskableGraphic : Graphic : UIBehaviour : MonoBehaviour; it is kept so tools/check-stubs.ps1 can compare base types.
using UnityEngine;

namespace UnityEngine.EventSystems
{
    public abstract class UIBehaviour : MonoBehaviour { }
}

namespace UnityEngine.UI
{
    public abstract class Graphic : UnityEngine.EventSystems.UIBehaviour { }

    public abstract class MaskableGraphic : Graphic { }

    public class Text : MaskableGraphic
    {
        public virtual string text { get { throw null; } set { throw null; } }
    }
}
