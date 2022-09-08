using UnityEngine;
using UnityEngine.EventSystems;

namespace Com.TheFallenGames.OSA.Core.SubComponents
{
	public class NavigationManager<TParams, TItemViewsHolder>
		where TParams : BaseParams
		where TItemViewsHolder : BaseItemViewsHolder
	{
		protected OSA<TParams, TItemViewsHolder> Adapter { get { return _Adapter; } }
		protected GameObject LastSelectedObject { get { return _LastSelectedObject; } }

		protected BaseParams.NavigationParams NavParams { get { return _Adapter.Parameters.Navigation; } }

		readonly OSA<TParams, TItemViewsHolder> _Adapter;
		GameObject _LastSelectedObject;
		float _DurationOfLastAnimatedBringToView;
		float _OSATimeOnLastAnimatedBringToView;


		public NavigationManager(OSA<TParams, TItemViewsHolder> adapter)
		{
			_Adapter = adapter;
		}


		public virtual void OnUpdate()
		{
			if (!NavParams.Enabled)
			{
				_LastSelectedObject = null;
				return;
			}

			var curSelected = GetCurrentlySelectedObject();
			if (!curSelected)
			{
				_LastSelectedObject = null;
				return;
			}

			if (_LastSelectedObject != curSelected)
			{
				if (curSelected)
					OnNewObjectSelected(curSelected);

				_LastSelectedObject = curSelected;
			}
		}

		protected virtual GameObject GetCurrentlySelectedObject()
		{
			if (!EventSystem.current)
				return null;

			return EventSystem.current.currentSelectedGameObject;
		}

		protected virtual float GetMaxInputModuleActionsPerSecondToExpect()
		{
			if (!EventSystem.current)
				return 1f;

			if (!EventSystem.current.currentInputModule)
				return 1f;

			var standaloneInputModule = EventSystem.current.currentInputModule as StandaloneInputModule;
			if (!standaloneInputModule)
				return 1f;

			return standaloneInputModule.inputActionsPerSecond;
		}

		protected virtual void OnNewObjectSelected(GameObject curSelected)
		{
			var vh = GetViewsHolderFromSelectedGameObject(curSelected);
			if (vh != null) 
				AssureItemFullyVisible(vh.ItemIndex);
		}

		protected virtual AbstractViewsHolder GetViewsHolderFromSelectedGameObject(GameObject curSelected)
		{
			int currentDepth = 0;
			int maxDepth = NavParams.MaxSearchDepthForViewsHolder;
			var curTR = curSelected.transform;
			while (curTR && currentDepth <= maxDepth)
			{
				var vh = GetViewsHolderFromRoot(curTR as RectTransform);
				if (vh != null)
				{
					return vh;
				}

				curTR = curTR.parent;
				++currentDepth;
			}

			return null;
		}

		protected virtual AbstractViewsHolder GetViewsHolderFromRoot(RectTransform root)
		{
			return _Adapter.GetItemViewsHolderIfVisible(root);
		}

		//protected virtual float GetItemSize(int itemIndex)
		//{
		//	var indexInView = _Adapter._ItemsDesc.GetItemViewIndexFromRealIndexChecked(itemIndex);
		//	return (float)_Adapter._ItemsDesc.GetItemSizeOrDefault(indexInView);
		//}

		protected void AssureItemFullyVisible(int itemIndex)
		{
			var param = _Adapter.Parameters;
			// Specifying a bigger distance from the edge so that the next item will become visible and thus the EventSystem will be able to select it.
			// To allow continuous scrolling when keeping the direction button pressed, we're making ~half of the next item visible 
			// and in a Low-FPS scenario we do a non-animated (immediate) BringToView.
			
			// Commented: using the default item size for the base value of calculating the distance from vp edge, 
			// so that we'll have the same distance from the edge when scrolling multiple times, regardless of item's size
			//float itemSize = GetItemSize(itemIndex);
			float itemSize = param.DefaultItemSize;
			var spaceFromViewportEdge = itemSize / 2f;

			// Make sure to have some decent addtional distance, so that the next item will become visible
			if (spaceFromViewportEdge < param.ContentSpacing)
				spaceFromViewportEdge = param.ContentSpacing;

			// Add ContentSpacing
			spaceFromViewportEdge += param.ContentSpacing;

			float duration = NavParams.ScrollDuration;
			duration = Mathf.Clamp01(duration);
			float maxNavigationsPerSecToExpect = GetMaxInputModuleActionsPerSecondToExpect();
			float maxNavigationsFrequency = 1f / maxNavigationsPerSecToExpect;

			// Also, this makes sure a continuous scroll will be possible, by making it impossible (in the general case) to select objects faster than the nav animation 
			// (at least when the input button/control is kept pressed)
			duration = Mathf.Clamp(duration, 0f, maxNavigationsFrequency - 0.05f);

			float durationToUse;
			if (duration == 0f || _Adapter.DeltaTime > duration / 2f)
			{
				durationToUse = 0f;
			}
			else
			{
				float timeSinceLastBringToView = _Adapter.Time - _OSATimeOnLastAnimatedBringToView;
				if (timeSinceLastBringToView < 0f)
					timeSinceLastBringToView = 0f;

				if (timeSinceLastBringToView < _DurationOfLastAnimatedBringToView)
				{
					// Last nav animation didn't finish => do an immediate jump
					durationToUse = 0f;
				}
				else
				{
					durationToUse = duration;
				}
			}

			_DurationOfLastAnimatedBringToView = durationToUse;
			if (durationToUse == 0f)
			{
				_OSATimeOnLastAnimatedBringToView = 0f;
				if (NavParams.Centered)
					_Adapter.ScrollTo(itemIndex, .5f, .5f);
				else
					_Adapter.BringToView(itemIndex, spaceFromViewportEdge);
			}
			else
			{
				_OSATimeOnLastAnimatedBringToView = _Adapter.Time;
				if (NavParams.Centered)
					_Adapter.SmoothScrollTo(itemIndex, durationToUse, .5f, .5f, null, null, true);
				else
					_Adapter.SmoothBringToView(itemIndex, durationToUse, spaceFromViewportEdge, null, null, true);
			}
		}
	}
}
