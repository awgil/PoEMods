using Patchwork.Attributes;
using UnityEngine;

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

    // another source of flickering is the anchor; sometimes it shifts coordinates of UI elements by 1 back and forth
    // exact reason is unknown; but to fix it, I revert local transform changes due to anchoring if they are <= 1 - this seems to work ok
    [ModifiesType]
    public class mod_UIAnchor : UIAnchor
    {
        // reference implementation
        //[ModifiesMember]
        //public new Vector3 GetPosition()
        //{
        //    if (!this.mTrans)
        //        this.mTrans = base.transform;

        //    bool anchorToCamera = false;
        //    if (this.panelContainer != null)
        //    {
        //        if (this.panelContainer.clipping != UIDrawCall.Clipping.None)
        //        {
        //            Vector4 panelClipRange = this.panelContainer.clipRange;
        //            this.mRect.x = panelClipRange.x - panelClipRange.z * 0.5f;
        //            this.mRect.y = panelClipRange.y - panelClipRange.w * 0.5f;
        //            this.mRect.width = panelClipRange.z;
        //            this.mRect.height = panelClipRange.w;
        //        }
        //        else
        //        {
        //            float single = (this.mRoot == null ? 0.5f : (float)this.mRoot.activeHeight / (float)Screen.height * 0.5f);
        //            this.mRect.xMin = (float)(-Screen.width) * single;
        //            this.mRect.yMin = (float)(-Screen.height) * single;
        //            this.mRect.xMax = -this.mRect.xMin;
        //            this.mRect.yMax = -this.mRect.yMin;
        //        }
        //    }
        //    else if (this.widgetContainer != null)
        //    {
        //        Transform transforms = this.widgetContainer.cachedTransform;
        //        Vector3 widgetLocalScale = transforms.localScale;
        //        Vector3 widgetLocalPos = transforms.localPosition;
        //        Vector3 widgetRelSize = this.widgetContainer.relativeSize;
        //        Vector3 widgetPivotOffset = this.widgetContainer.pivotOffset;
        //        widgetPivotOffset.y -= 1f;
        //        widgetPivotOffset.x = widgetPivotOffset.x * (widgetRelSize.x * widgetLocalScale.x);
        //        widgetPivotOffset.y = widgetPivotOffset.y * (widgetRelSize.y * widgetLocalScale.y);
        //        this.mRect.x = widgetLocalPos.x + widgetPivotOffset.x;
        //        this.mRect.y = widgetLocalPos.y + widgetPivotOffset.y;
        //        this.mRect.width = widgetRelSize.x * widgetLocalScale.x;
        //        this.mRect.height = widgetRelSize.y * widgetLocalScale.y;
        //    }
        //    else if (this.uiCamera != null)
        //    {
        //        anchorToCamera = true;
        //        this.mRect = this.uiCamera.pixelRect;
        //    }
        //    else
        //    {
        //        return Vector3.zero;
        //    }

        //    float xMid = (this.mRect.xMin + this.mRect.xMax) * 0.5f;
        //    float yMid = (this.mRect.yMin + this.mRect.yMax) * 0.5f;
        //    Vector3 result = new Vector3(xMid, yMid, 0f);
        //    if (this.side != UIAnchor.Side.Center)
        //    {
        //        if (this.side == UIAnchor.Side.Right || this.side == UIAnchor.Side.TopRight || this.side == UIAnchor.Side.BottomRight)
        //        {
        //            result.x = this.mRect.xMax;
        //        }
        //        else if (this.side == UIAnchor.Side.Top || this.side == UIAnchor.Side.Center || this.side == UIAnchor.Side.Bottom)
        //        {
        //            result.x = xMid;
        //        }
        //        else
        //        {
        //            result.x = this.mRect.xMin;
        //        }
        //        if (this.side == UIAnchor.Side.Top || this.side == UIAnchor.Side.TopRight || this.side == UIAnchor.Side.TopLeft)
        //        {
        //            result.y = this.mRect.yMax;
        //        }
        //        else if (this.side == UIAnchor.Side.Left || this.side == UIAnchor.Side.Center || this.side == UIAnchor.Side.Right)
        //        {
        //            result.y = yMid;
        //        }
        //        else
        //        {
        //            result.y = this.mRect.yMin;
        //        }
        //    }
        //    result.x = result.x + this.relativeOffset.x * this.mRect.width;
        //    result.y = result.y + this.relativeOffset.y * this.mRect.height;
        //    if (!anchorToCamera)
        //    {
        //        result.x = Mathf.Round(result.x);
        //        result.y = Mathf.Round(result.y);
        //        result.x += this.pixelOffset.x;
        //        result.y += this.pixelOffset.y;
        //        if (this.panelContainer != null)
        //        {
        //            result = this.panelContainer.cachedTransform.TransformPoint(result);
        //        }
        //        else if (this.widgetContainer != null)
        //        {
        //            Transform transforms1 = this.widgetContainer.cachedTransform.parent;
        //            if (transforms1 != null)
        //            {
        //                result = transforms1.TransformPoint(result);
        //            }
        //        }
        //    }
        //    else
        //    {
        //        if (this.uiCamera.orthographic)
        //        {
        //            result.x = Mathf.Round(result.x);
        //            result.y = Mathf.Round(result.y);
        //            result.x += this.pixelOffset.x;
        //            result.y += this.pixelOffset.y;
        //        }
        //        Vector3 screenPoint = this.uiCamera.WorldToScreenPoint(this.mTrans.position);
        //        result.z = screenPoint.z;
        //        result = this.uiCamera.ScreenToWorldPoint(result);
        //    }
        //    if (this.DisableX)
        //    {
        //        result.x = this.mTrans.position.x;
        //    }
        //    if (this.DisableY)
        //    {
        //        result.y = this.mTrans.position.y;
        //    }
        //    return result;
        //}

        [ModifiesMember]
        public new void Update()
        {
            if (this.mAnim != null && this.mAnim.enabled && this.mAnim.isPlaying)
            {
                return;
            }

            Vector3 position = this.GetPosition();
            Vector3 oldLocalPos = this.mTrans.localPosition;

            if (this.mTrans.position != position)
            {
                this.mTrans.position = position;
            }

            Vector3 newLocalPos = this.mTrans.localPosition;
            newLocalPos.z = oldLocalPos.z;
            if (this.RoundToWhole)
            {
                newLocalPos.x = Mathf.Floor(this.mTrans.localPosition.x);
                newLocalPos.y = Mathf.Floor(this.mTrans.localPosition.y);
            }

            // this is my hack ...
            float dx = Mathf.Abs(oldLocalPos.x - newLocalPos.x);
            float dy = Mathf.Abs(oldLocalPos.y - newLocalPos.y);
            if (dx <= 1 && dy <= 1)
            {
                newLocalPos.x = oldLocalPos.x;
                newLocalPos.y = oldLocalPos.y;
            }
            // ... and here it ends

            if (newLocalPos != this.mTrans.localPosition)
            {
                this.mTrans.localPosition = newLocalPos;
            }

            if (this.runOnlyOnce && Application.isPlaying)
            {
                Object.Destroy(this);
            }
        }
    }
}
