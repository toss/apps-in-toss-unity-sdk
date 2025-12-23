class Permit {
  constructor(onRelease) {
    this.isReleased = false;
    this.onRelease = onRelease;
  }
  async release() {
    if (!this.isReleased) {
      this.isReleased = true;
      await this.onRelease();
    }
  }
}
class Semaphore {
  constructor(count) {
    this.deferreds = [];
    this.availablePermits = count;
  }
  acquire() {
    if (this.availablePermits > 0) {
      return Promise.resolve(this.createPermit());
    } else {
      const deferred = {};
      deferred.promise = new Promise(resolve => {
        deferred.resolve = resolve;
      });
      this.deferreds.push(deferred);
      return deferred.promise;
    }
  }
  createPermit() {
    this.availablePermits--;
    return new Permit(async () => {
      this.availablePermits++;
      if (this.deferreds.length > 0) {
        const deferred = this.deferreds.shift();
        deferred.resolve(this.createPermit());
        await deferred.promise;
      }
    });
  }
}

export { Permit, Semaphore };
