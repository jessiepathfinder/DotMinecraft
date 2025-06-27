using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace DotMinecraft
{
	public static class NoGCRegionsController
	{
		private static readonly ConcurrentBag<TaskCompletionSource<bool>> enterRequests = new();
		private static readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(0);
		private static void NoGCRegionControllerThread(){
			SemaphoreSlim ss = semaphoreSlim;
			ConcurrentBag<TaskCompletionSource<bool>> er = enterRequests;
			ulong noGcCounter = 0;
			bool isInNoGcRegion = false;
			while(true){
				ss.Wait();
				if(er.TryTake(out TaskCompletionSource<bool>? tsc)){
					if (tsc is null) throw new Exception("Unexpected null task completion source (should not reach here)");
					if(noGcCounter == 0 & !isInNoGcRegion){
						try{
							isInNoGcRegion = GC.TryStartNoGCRegion(268435456, 251658240, false);
						} catch(InvalidOperationException){
							//NOTE that TryStartNoGCRegion throws InvalidOperationException if another
							//thread puts us in no-gc region first
							//This should only happen when some "wierd" non-compliant libraries
							//trigger a no-gc region
							//We need to own the no-gc region in order to be able to
							//release it later on
						}
						tsc.SetResult(isInNoGcRegion);
						if (isInNoGcRegion){
							noGcCounter = 1;
						}
						continue;
					}
					++noGcCounter;

					tsc.SetResult(true);
				} else{
					//Note: no-gc regions are a courtesy, not a guarantee
					if (noGcCounter == 0) continue;
					--noGcCounter;
					if (noGcCounter == 0 & isInNoGcRegion){
						try{
							GC.EndNoGCRegion();
						} catch (InvalidOperationException){
							//Another thread exited the no-gc region without our permission!
						}
						isInNoGcRegion = false;
					}
				}
			}
		}
		static NoGCRegionsController(){
			Thread thread = new Thread(NoGCRegionControllerThread, 131072);
			thread.IsBackground = true;
			thread.Priority = ThreadPriority.Highest;
			thread.Start();
		}
		private static readonly Task<bool> failed = Task.FromResult(false);
		public static Task<bool> EnterNoGCRegionAsync(){
			try{
				TaskCompletionSource<bool> tsc = new();
				enterRequests.Add(tsc);
				semaphoreSlim.Release();
				return tsc.Task;
			} catch{
				//This should be virtually impossible
				return failed;
			}
		}
		public static void ExitNoGCRegionAsync(){
			try{
				semaphoreSlim.Release();
			} catch{
				
			}
		}
	}
	public sealed class NoGCRegionHandle : IDisposable
	{
		private volatile int doubleDisposeProtection;
		private readonly Task<bool> tsk;
		public NoGCRegionHandle(){
			tsk = NoGCRegionsController.EnterNoGCRegionAsync();
		}
		public void Dispose()
		{
			if (Interlocked.Exchange(ref doubleDisposeProtection, 1) == 0) {
				GC.SuppressFinalize(this);
				tsk.GetAwaiter().OnCompleted(N);
			}
		}
		private void N(){
			if(tsk.Result){
				NoGCRegionsController.ExitNoGCRegionAsync();
			}
		}
		~NoGCRegionHandle(){
			if (!AppDomain.CurrentDomain.IsFinalizingForUnload()) throw new Exception("Rouge NoGCRegionHandle not disposed properly");
		}
		
	}
}
