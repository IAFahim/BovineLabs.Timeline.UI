using BovineLabs.Timeline.UI.Data.ViewModel;
using NUnit.Framework;

namespace BovineLabs.Timeline.UI.Tests
{
    /// <summary>
    /// Regression: the debug toolbar constructs <c>RowsView</c> (and reads the Rows binding) before
    /// any track system has called <c>Load()</c> via UIHelper.Bind. Reading <see cref="RowsViewModel.Rows"/>
    /// on an unloaded VM used to NRE inside MultiContainer (NativeList.AsReadOnly on an uncreated list).
    /// </summary>
    public class RowsViewModelTests
    {
        [Test]
        public void Rows_BeforeLoad_ReturnsNullInsteadOfThrowing()
        {
            var vm = new RowsViewModel();

            Assert.DoesNotThrow(() => _ = vm.Rows);
            Assert.IsNull(vm.Rows);
        }

        [Test]
        public void Rows_AfterLoad_ReturnsBindableArray()
        {
            var vm = new RowsViewModel();
            vm.Load();

            try
            {
                Assert.IsNotNull(vm.Rows);
                Assert.AreEqual(0, vm.Rows.Count);
            }
            finally
            {
                vm.Unload();
            }
        }

        [Test]
        public void Rows_AfterUnload_ReturnsNullAgain()
        {
            var vm = new RowsViewModel();
            vm.Load();
            vm.Unload();

            Assert.IsNull(vm.Rows);
        }
    }
}
