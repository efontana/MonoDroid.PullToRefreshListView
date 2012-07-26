using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Views.Animations;

using MonoAndroidPullToRefresh;

namespace MonoAndroid.PullToRefresh
{
  public class PullToRefreshListView : ListView, AbsListView.IOnScrollListener
  {
    private static int TAP_TO_REFRESH = 1;
    private static int PULL_TO_REFRESH = 2;
    private static int RELEASE_TO_REFRESH = 3;
    public static int REFRESHING = 4;
    
    private OnRefreshListener mOnRefreshListener;

    private IOnScrollListener mOnScrollListener;

    private LayoutInflater mInflater;

    private RelativeLayout mRefreshView;
    private TextView mRefreshViewText;
    private ImageView mRefreshViewImage;
    private ProgressBar mRefreshViewProgress;
    private TextView mRefreshViewLastUpdated;

    private Android.Widget.ScrollState mCurrentScrollState;
    private int mRefreshState;

    private RotateAnimation mFlipAnimation;
    private RotateAnimation mReverseFlipAnimation;

    private int mRefreshViewHeight;
    private int mRefreshOriginalTopPadding;
    private int mLastMotionY;

    private bool mBounceHack;

    public PullToRefreshListView(Context context)
      : base(context)
    {
      Initialize();
    }

    public PullToRefreshListView(Context context, IAttributeSet attrs) :
      base(context, attrs)
    {
      Initialize();
    }

    public PullToRefreshListView(Context context, IAttributeSet attrs, int defStyle) :
      base(context, attrs, defStyle)
    {
      Initialize();
    }

    private void Initialize()
    {
      mFlipAnimation = new RotateAnimation(0, -180,
                Android.Views.Animations.Dimension.RelativeToSelf, 0.5f,
                Android.Views.Animations.Dimension.RelativeToSelf, 0.5f);


      mFlipAnimation.Interpolator = new LinearInterpolator();

      mFlipAnimation.Duration = 250;
      mFlipAnimation.FillAfter = true;

      mReverseFlipAnimation = new RotateAnimation(-180, 0,
              Android.Views.Animations.Dimension.RelativeToSelf, 0.5f,
              Android.Views.Animations.Dimension.RelativeToSelf, 0.5f);

      mReverseFlipAnimation.Interpolator = new LinearInterpolator();
      mReverseFlipAnimation.Duration = 250;
      mReverseFlipAnimation.FillAfter = true;


      mInflater = Context.GetSystemService(Context.LayoutInflaterService) as LayoutInflater;

      mRefreshView = (RelativeLayout)mInflater.Inflate(Resource.Layout.pull_to_refresh_header, this, false);

      mRefreshViewText =
          (TextView)mRefreshView.FindViewById(Resource.Id.pull_to_refresh_text);
      mRefreshViewImage =
          (ImageView)mRefreshView.FindViewById(Resource.Id.pull_to_refresh_image);
      mRefreshViewProgress =
          (ProgressBar)mRefreshView.FindViewById(Resource.Id.pull_to_refresh_progress);
      mRefreshViewLastUpdated =
          (TextView)mRefreshView.FindViewById(Resource.Id.pull_to_refresh_updated_at);

      mRefreshViewImage.SetMinimumHeight(50);
      
      mRefreshView.SetOnClickListener(new OnClickRefreshListener(this));

      mRefreshOriginalTopPadding = mRefreshView.PaddingTop;

      mRefreshState = TAP_TO_REFRESH;

      AddHeaderView(mRefreshView);

      base.SetOnScrollListener(this);


      MeasureView(mRefreshView);
      mRefreshViewHeight = mRefreshView.MeasuredHeight;
    }

    protected override void OnAttachedToWindow()
    {
      base.OnAttachedToWindow();
      SetSelection(1);
    }

    public override IListAdapter Adapter
    {
      set
      {
        base.Adapter = value;
        SetSelection(1);
      }
    }

    public override void SetOnScrollListener(IOnScrollListener l)
    {
      mOnScrollListener = l;
    }

    public void SetOnRefreshListener(OnRefreshListener listener)
    {
      mOnRefreshListener = listener;
    }

    public void SetLastUpdated(string lastUpdated)
    {
      if (lastUpdated != null)
      {
        mRefreshViewLastUpdated.Visibility = ViewStates.Visible;
        mRefreshViewLastUpdated.Text = lastUpdated;
      }
      else
        mRefreshViewLastUpdated.Visibility = ViewStates.Gone;
    }

    public override bool OnTouchEvent(MotionEvent e)
    {
      int y = (int)e.GetY();
      mBounceHack = false;

      switch (e.Action)
      {
        case MotionEventActions.Up:
          if (!VerticalScrollBarEnabled)
            VerticalScrollBarEnabled = true;

          if (FirstVisiblePosition == 0 && mRefreshState != REFRESHING)
          {
            if ((mRefreshView.Bottom >= mRefreshViewHeight
                    || mRefreshView.Top >= 0)
                    && mRefreshState == RELEASE_TO_REFRESH)
            {
              // Initiate the refresh
              mRefreshState = REFRESHING;
              PrepareForRefresh();
              onRefresh();
            }
            else if (mRefreshView.Bottom < mRefreshViewHeight
                  || mRefreshView.Top <= 0)
            {
              // Abort refresh and scroll down below the refresh view
              ResetHeader();
              SetSelection(1);
            }
          }
          break;
        case MotionEventActions.Down:
          mLastMotionY = y;
          break;
        case MotionEventActions.Move:
          ApplyHeaderPadding(e);
          break;
      }

      return base.OnTouchEvent(e);
    }

    private void ApplyHeaderPadding(MotionEvent ev)
    {
      int pointerCount = ev.HistorySize;
      for (int p = 0; p < pointerCount; p++)
      {
        if (mRefreshState == RELEASE_TO_REFRESH)
        {
          if (VerticalFadingEdgeEnabled)
            VerticalScrollBarEnabled = false;

          int historicalY = (int)ev.GetHistoricalY(p);
          int topPadding = (int)(((historicalY - mLastMotionY)
                      - mRefreshViewHeight) / 1.7);

          mRefreshView.SetPadding(
            mRefreshView.PaddingLeft,
            topPadding,
            mRefreshView.PaddingRight,
            mRefreshView.PaddingBottom);
        }
      }
    }

    private void ResetHeaderPadding()
    {
      mRefreshView.SetPadding(
             mRefreshView.PaddingLeft,
             mRefreshOriginalTopPadding,
             mRefreshView.PaddingRight,
             mRefreshView.PaddingBottom);
    }

    private void ResetHeader()
    {
      if (mRefreshState != TAP_TO_REFRESH)
      {
        mRefreshState = TAP_TO_REFRESH;
        ResetHeaderPadding();

        // Set refresh view text to the pull label
        mRefreshViewText.SetText(Resource.String.pull_to_refresh_tap_label);
        // Replace refresh drawable with arrow drawable
        mRefreshViewImage.SetImageResource(Resource.Drawable.ic_pulltorefresh_arrow);
        // Clear the full rotation animation
        mRefreshViewImage.ClearAnimation();
        // Hide progress bar and arrow.
        mRefreshViewImage.Visibility = ViewStates.Gone;
        mRefreshViewProgress.Visibility = ViewStates.Gone;
      }
    }

    private void MeasureView(View child)
    {
      ViewGroup.LayoutParams p = child.LayoutParameters;
      if (p == null)
        p = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.FillParent, ViewGroup.LayoutParams.WrapContent);

      int childWidthSpec = ViewGroup.GetChildMeasureSpec(0, 0 + 0, p.Width);
      int lpHeight = p.Height;
      int childHeightSpec;
      if (lpHeight > 0)
      {
        childHeightSpec = MeasureSpec.MakeMeasureSpec(lpHeight, MeasureSpecMode.Exactly);
      }
      else
      {
        childHeightSpec = MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified);
      }
      child.Measure(childWidthSpec, childHeightSpec);
    }

    public void OnScroll(AbsListView view, int firstVisibleItem, int visibleItemCount, int totalItemCount)
    {
      // When the refresh view is completely visible, change the text to say
      // "Release to refresh..." and flip the arrow drawable.
      if (mCurrentScrollState == Android.Widget.ScrollState.TouchScroll && mRefreshState != REFRESHING)
      {
        if (firstVisibleItem == 0)
        {
          mRefreshViewImage.Visibility = ViewStates.Visible;
          if ((mRefreshView.Bottom >= mRefreshViewHeight + 20
                  || mRefreshView.Top >= 0)
                  && mRefreshState != RELEASE_TO_REFRESH)
          {
            mRefreshViewText.SetText(Resource.String.pull_to_refresh_release_label);
            mRefreshViewImage.ClearAnimation();
            mRefreshViewImage.StartAnimation(mFlipAnimation);
            mRefreshState = RELEASE_TO_REFRESH;
          }
          else if (mRefreshView.Bottom < mRefreshViewHeight + 20
                && mRefreshState != PULL_TO_REFRESH)
          {
            mRefreshViewText.SetText(Resource.String.pull_to_refresh_pull_label);
            if (mRefreshState != TAP_TO_REFRESH)
            {
              mRefreshViewImage.ClearAnimation();
              mRefreshViewImage.StartAnimation(mReverseFlipAnimation);
            }
            mRefreshState = PULL_TO_REFRESH;
          }
        }
        else
        {
          mRefreshViewImage.Visibility = ViewStates.Gone;
          ResetHeader();
        }
      }
      else if (mCurrentScrollState == ScrollState.Fling
            && firstVisibleItem == 0
            && mRefreshState != REFRESHING)
      {
        SetSelection(1);
        mBounceHack = true;
      }
      else if (mBounceHack && mCurrentScrollState == ScrollState.Fling)
      {
        SetSelection(1);
      }

      if (mOnScrollListener != null)
      {       
        mOnScrollListener.OnScroll(view, firstVisibleItem, visibleItemCount, totalItemCount);
      }
    }

    public void OnScrollStateChanged(AbsListView view, ScrollState scrollState)
    {
      mCurrentScrollState = scrollState;

      if (mCurrentScrollState == ScrollState.Idle)
      {
        mBounceHack = false;
      }

      if (mOnScrollListener != null)
      {
        mOnScrollListener.OnScrollStateChanged(view, scrollState);
      }
    }

     public void PrepareForRefresh() {
        ResetHeaderPadding();

        mRefreshViewImage.Visibility = ViewStates.Gone;
        // We need this hack, otherwise it will keep the previous drawable.
     
        mRefreshViewImage.SetImageDrawable(null);
        mRefreshViewProgress.Visibility = ViewStates.Visible;

        // Set refresh view text to the refreshing label
        mRefreshViewText.SetText(Resource.String.pull_to_refresh_refreshing_label);

        mRefreshState = REFRESHING;
    }

     public void onRefresh()
     {
       if (mOnRefreshListener != null)
       {
         mOnRefreshListener.onRefresh();
       }
     }

     public void onRefreshComplete(string lastUpdated)
     {
       SetLastUpdated(lastUpdated);
       onRefreshComplete();
     }

    public void onRefreshComplete()
    {
      ResetHeader();

      if (mRefreshView.Bottom > 0)
      {
        InvalidateViews();
        SetSelection(1);
      }
    }

    private class OnClickRefreshListener : Java.Lang.Object, IOnClickListener
    {
      PullToRefreshListView listView;
      public OnClickRefreshListener(PullToRefreshListView lv)
      {
        listView = lv;
      }

      public void OnClick(View v)
      {
        if (listView.mRefreshState != PullToRefreshListView.REFRESHING)
        {
          listView.PrepareForRefresh();
          listView.onRefresh();
        }
      }
    
    }

    public interface OnRefreshListener
    {
      void onRefresh();
    }


  }
}