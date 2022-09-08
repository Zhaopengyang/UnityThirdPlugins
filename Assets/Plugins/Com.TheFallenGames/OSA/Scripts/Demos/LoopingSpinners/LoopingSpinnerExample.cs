using System;
using UnityEngine;
using UnityEngine.UI;
using Com.TheFallenGames.OSA.Core;
using Com.TheFallenGames.OSA.CustomParams;

namespace Com.TheFallenGames.OSA.Demos.LoopingSpinners
{
	/// <summary>
	/// Very basic example with a spinner that loops its items, similarly to a time picker in an alarm app.
	/// Minimal implementation of the adapter that initializes and updates the viewsholders. The size of each item is fixed in this case and it's the same as the prefab's
	/// </summary>
	public class LoopingSpinnerExample : OSA<MyParams, MyItemViewsHolder>
	{
		// Keeping the approx. minimum required items to prevent gaps in the viewport (otherwise we'd have to duplicate items or simply stop looping)
		public const int MIN_ITEMS = 4;


		#region OSA implementation
		/// <inheritdoc/>
		protected override void Update()
        {
			base.Update();

			if (!IsInitialized)
				return;

			_Params.currentSelectedIndicatorText.text = "Selected: ";
            if (VisibleItemsCount == 0)
                return;

            int middleVHIndex = VisibleItemsCount / 2;
            var middleVH = GetItemViewsHolder(middleVHIndex);

            _Params.currentSelectedIndicatorText.text += _Params.GetItemValueAtIndex(middleVH.ItemIndex);
            middleVH.background.CrossFadeColor(_Params.selectedColor, .1f, false, false);

            for (int i = 0; i < VisibleItemsCount; ++i)
            {
                if (i != middleVHIndex)
					GetItemViewsHolder(i).background.CrossFadeColor(_Params.nonSelectedColor, .1f, false, false);
            }
		}

		/// <inheritdoc/>
		protected override MyItemViewsHolder CreateViewsHolder(int itemIndex)
		{
			var instance = new MyItemViewsHolder();
			instance.Init(_Params.ItemPrefab, _Params.Content, itemIndex);

			return instance;
		}

		/// <inheritdoc/>
		protected override void UpdateViewsHolder(MyItemViewsHolder newOrRecycled) { newOrRecycled.titleText.text = _Params.GetItemValueAtIndex(newOrRecycled.ItemIndex) + ""; }
		#endregion

		public void ChangeItemsCountWithChecks(int newCount)
		{
			if (newCount < MIN_ITEMS)
				newCount = MIN_ITEMS;

			ResetItems(newCount);
		}
	}


	[Serializable] // serializable, so it can be shown in inspector
	public class MyParams : BaseParamsWithPrefab
	{
		public int startItemNumber = 0;
		public int increment = 1;
		public Color selectedColor, nonSelectedColor;
		public Text currentSelectedIndicatorText;

		/// <summary>The value of each item is calculated dynamically using its <paramref name="index"/>, <see cref="startItemNumber"/> and the <see cref="increment"/></summary>
		/// <returns>The item's value (the displayed number)</returns>
		public int GetItemValueAtIndex(int index) { return startItemNumber + increment * index; }
	}


	public class MyItemViewsHolder : BaseItemViewsHolder
	{
		public Image background;
		public Text titleText;

		/// <inheritdoc/>
		public override void CollectViews()
		{
			base.CollectViews();

			background = root.GetComponent<Image>();
			titleText = root.GetComponentInChildren<Text>();
		}
	}
}