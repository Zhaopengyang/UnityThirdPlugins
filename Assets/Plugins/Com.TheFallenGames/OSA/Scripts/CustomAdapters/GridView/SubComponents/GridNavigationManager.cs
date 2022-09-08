using Com.TheFallenGames.OSA.CustomAdapters.GridView;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Com.TheFallenGames.OSA.Core.SubComponents
{
	public class GridNavigationManager<TParams, TCellViewsHolder> : NavigationManager<TParams, CellGroupViewsHolder<TCellViewsHolder>>
		where TParams : GridParams
		where TCellViewsHolder : CellViewsHolder, new()
	{
		protected GridAdapter<TParams, TCellViewsHolder> GridAdapter { get { return _GridAdapter; } }

		readonly GridAdapter<TParams, TCellViewsHolder> _GridAdapter;


		public GridNavigationManager(GridAdapter<TParams, TCellViewsHolder> gridAdapter) : base(gridAdapter)
		{
			_GridAdapter = gridAdapter;
		}


		protected override AbstractViewsHolder GetViewsHolderFromRoot(RectTransform root)
		{
			return _GridAdapter.GetCellViewsHolderIfVisible(root);
		}

		// TODO may also add options to navigate transversally to the OSA's direction
	}
}
