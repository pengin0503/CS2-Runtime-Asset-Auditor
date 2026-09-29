interface StubBinding<T> {
  readonly value: T;
  subscribe(listener?: (value: T) => void): { dispose(): void };
  dispose(): void;
}

// Tests can publish a binding value (for example the game locale) before rendering.
const published = new Map<string, unknown>();

export function setBindingValue(group: string, name: string, value: unknown): void {
  published.set(`${group}/${name}`, value);
}

export function clearBindingValues(): void {
  published.clear();
}

export function bindValue<T>(group: string, name: string, fallbackValue?: T): StubBinding<T> {
  const key = `${group}/${name}`;
  const read = () => (published.has(key) ? published.get(key) : fallbackValue) as T;
  return {
    get value() { return read(); },
    subscribe(listener?: (value: T) => void) {
      if (listener) listener(read());
      return { dispose() {} };
    },
    dispose() {}
  };
}

export function useValue<T>(binding: StubBinding<T>): T {
  return binding.value;
}

export function trigger(_group: string, _name: string, ..._args: unknown[]): void {}
