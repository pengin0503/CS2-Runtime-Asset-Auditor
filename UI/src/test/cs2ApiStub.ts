interface StubBinding<T> {
  readonly value: T;
  subscribe(listener?: (value: T) => void): { dispose(): void };
  dispose(): void;
}

export function bindValue<T>(_group: string, _name: string, fallbackValue?: T): StubBinding<T> {
  return {
    value: fallbackValue as T,
    subscribe(listener?: (value: T) => void) {
      if (listener) listener(fallbackValue as T);
      return { dispose() {} };
    },
    dispose() {}
  };
}

export function useValue<T>(binding: StubBinding<T>): T {
  return binding.value;
}

export function trigger(_group: string, _name: string, ..._args: unknown[]): void {}
