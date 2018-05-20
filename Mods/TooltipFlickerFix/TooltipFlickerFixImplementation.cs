using Patchwork.Attributes;

namespace TooltipFlickerFix
{
    // tooltip flickers, because UITable's coordinates shift by 1 back and forth every frame
    // I think it is caused by small floating-point rounding errors when calculating world-to-local matrix, which become not so small when rounded to integer
    [ModifiesType]
    public class mod_UITable : UITable
    {
        [NewMember]
        [DuplicatesBody("Reposition")]
        public void ori_Reposition() { }

        [ModifiesMember("Reposition")]
        public void mod_Reposition()
        {
            // force disable "round to whole" mode, because it causes flickering:
            // small differences in floating-point calculations sometimes cause rounding to different int (+- 1 of original)
            // on next frame, local transform change causes new floating-point result, which rounds to original int again
            roundToWhole = false;
            ori_Reposition();
        }

        // this is the reference implementation, with locals renamed for clarity and useless temporaries moved back to expressions
        // also two-dimensional array is replaced with one-dimensional, since Patchwork crashes otherwise
        //[ModifiesMember]
        //private new void RepositionVariableSize()
        //{
        //    float x = 0f;
        //    float y = 0f;
        //    int numRows = (this.columns <= 0 ? 1 : children.Count / this.columns + 1);
        //    int numCols = (this.columns <= 0 ? children.Count : Mathf.Min(children.Count, this.columns));
        //    Bounds[] boundsChild = new Bounds[children.Count];
        //    Bounds[] boundsCols = new Bounds[numCols];
        //    Bounds[] boundsRows = new Bounds[numRows];

        //    int iCol = 0;
        //    int iRow = 0;
        //    this.m_calcHeight = -1f;
        //    for (int i = 0; i < children.Count; i++)
        //    {
        //        Transform child = children[i];
        //        UITable childTable = child.GetComponent<UITable>();
        //        if (childTable)
        //            childTable.Reposition();

        //        Bounds curBound = NGUIMath.CalculateRelativeWidgetBounds(child);
        //        curBound.min = Vector3.Scale(curBound.min, child.localScale);
        //        curBound.max = Vector3.Scale(curBound.max, child.localScale);
        //        boundsChild[i] = curBound;
        //        boundsCols[iCol].Encapsulate(curBound);
        //        boundsRows[iRow].Encapsulate(curBound);
        //        if (++iCol >= this.columns && this.columns > 0)
        //        {
        //            iCol = 0;
        //            iRow++;
        //        }
        //    }

        //    iCol = 0;
        //    iRow = 0;
        //    for (int i = 0; i < children.Count; i++)
        //    {
        //        Transform child = children[i];
        //        Bounds curBound = boundsChild[i];
        //        Bounds colBound = boundsCols[iCol];
        //        Bounds rowBound = boundsRows[iRow];
        //        Vector3 curPos = child.localPosition;

        //        curPos.x = x + curBound.extents.x - curBound.center.x;
        //        curPos.x += curBound.min.x - colBound.min.x + this.padding.x;

        //        if (this.direction != UITable.Direction.Down)
        //            curPos.y = y + curBound.extents.y - curBound.center.y;
        //        else
        //            curPos.y = -y - curBound.extents.y - curBound.center.y;
        //        curPos.y += (curBound.max.y - curBound.min.y - rowBound.max.y + rowBound.min.y) * 0.5f - this.padding.y;

        //        x += colBound.max.x - colBound.min.x + this.padding.x * 2f;

        //        if (this.roundToWhole)
        //        {
        //            curPos.x = Mathf.Round(curPos.x);
        //            curPos.y = Mathf.Round(curPos.y);
        //        }

        //        child.localPosition = curPos;

        //        if (++iCol >= this.columns && this.columns > 0)
        //        {
        //            iCol = 0;
        //            iRow++;
        //            x = 0f;
        //            y += rowBound.size.y + this.padding.y * 2f;
        //            this.m_calcHeight = Mathf.Max(this.m_calcHeight, y);
        //        }
        //    }
        //}
    }
}
