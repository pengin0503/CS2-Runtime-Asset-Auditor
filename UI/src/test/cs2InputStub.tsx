import React from "react";

export interface ConsumerRegistration {
  actions: Record<string, () => void>;
  ignoreFocusState?: boolean;
}

const registrations: ConsumerRegistration[] = [];

/** Consumers rendered since the last reset, so tests can fire an input action the way the game would. */
export function renderedInputConsumers(): readonly ConsumerRegistration[] {
  return registrations;
}

export function resetInputConsumers(): void {
  registrations.length = 0;
}

// Test double for the vanilla input action consumer: renders children, records the consumed actions so tests
// can invoke them, and exposes their names as a data attribute for static markup tests.
export function InputActionConsumer({ actions, ignoreFocusState, children }: any) {
  registrations.push({ actions: actions ?? {}, ignoreFocusState });
  return <div data-input-actions={Object.keys(actions ?? {}).join(",")}>{children}</div>;
}
