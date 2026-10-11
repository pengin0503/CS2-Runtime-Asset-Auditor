declare module "cs2/api" {
  export interface ValueBinding<T> {
    readonly value: T;
    subscribe(listener?: (value: T) => void): { dispose(): void };
    dispose(): void;
  }
  export function bindValue<T>(group: string, name: string, fallbackValue?: T): ValueBinding<T>;
  export function useValue<T>(binding: ValueBinding<T>): T;
  export function trigger(group: string, name: string, ...args: unknown[]): void;
}

declare module "cs2/modding" {
  import type { ComponentType } from "react";
  export type ModuleRegistryAppend = ComponentType<Record<string, never>> | (() => JSX.Element);
  export type AppendHookTargets = "Menu" | "Editor" | "Game" | "GameTopLeft" | "GameTopRight" | "GameBottomRight";
  export interface ModuleRegistry {
    append(target: AppendHookTargets, component: ModuleRegistryAppend, index?: number): void;
  }
  export type ModRegistrar = (moduleRegistry: ModuleRegistry) => void;
}

declare module "cs2/ui" {
  import type { ButtonHTMLAttributes, PropsWithChildren, ReactElement, ReactNode } from "react";

  export interface ButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, "onSelect"> {
    variant?: "flat" | "primary" | "round" | "menu" | "icon" | "floating" | "default" | "text";
    selected?: boolean;
    src?: string;
    /** Draws `src` as a mask filled with the theme's --iconColor, the way vanilla glyph buttons do. */
    tinted?: boolean | string;
    tooltipLabel?: ReactNode;
    onSelect?: () => void;
    as?: "button" | "div";
  }

  export const Button: (props: PropsWithChildren<ButtonProps>) => JSX.Element;

  export interface TooltipProps {
    tooltip: ReactNode;
    disabled?: boolean;
    direction?: "up" | "down" | "left" | "right";
    alignment?: "start" | "center" | "end";
    children: ReactElement;
  }

  export const Tooltip: (props: TooltipProps) => JSX.Element;

  // Vanilla icon: a 26rem box (override the size with --size) holding either an <img> or, when
  // tinted, a mask filled with --iconColor so monochrome game glyphs follow the text color.
  export interface IconProps {
    src: string;
    tinted?: boolean | string;
    className?: string;
  }

  export const Icon: (props: PropsWithChildren<IconProps>) => JSX.Element;

  // Mirrors the official toolchain declaration of the vanilla scroll container, which draws a
  // draggable scrollbar track (Gameface renders no native scrollbars for overflow containers).
  export interface ScrollableProps {
    horizontal?: boolean;
    vertical?: boolean;
    trackVisibility?: "always" | "scrollable";
    smooth?: boolean;
    className?: string;
    style?: import("react").CSSProperties;
    onScroll?: () => void;
  }

  export const Scrollable: (props: PropsWithChildren<ScrollableProps>) => JSX.Element | null;

  // Vanilla dropdown (game-ui/common/input/dropdown, 1.6.2f1 index.js). `children` is the toggle, `content` the items.
  // The menu opens in a popup layer, closes on an outside mouse-down, and consumes the Back action while open.
  // Without `theme` the game's default dropdown theme is used.
  export interface DropdownProps {
    focusKey?: unknown;
    initialFocused?: unknown;
    theme?: Record<string, string>;
    content: ReactNode;
    alignment?: string;
    onToggle?: (visible: boolean) => void;
  }

  export const Dropdown: (props: PropsWithChildren<DropdownProps>) => JSX.Element;

  // Selecting an item calls onChange, or onToggleSelected when it is already `selected`, then closes the menu
  // unless closeOnSelect is false.
  export interface DropdownItemProps<T> {
    value: T;
    selected?: boolean;
    disabled?: boolean;
    focusKey?: unknown;
    className?: string;
    closeOnSelect?: boolean;
    onChange?: (value: T) => void;
    onToggleSelected?: (value: T) => void;
  }

  export function DropdownItem<T>(props: PropsWithChildren<DropdownItemProps<T>>): JSX.Element;

  // A Button that opens and closes the surrounding Dropdown, with the theme's open/closed indicator.
  export interface DropdownToggleProps extends Omit<ButtonProps, "onSelect"> {
    showHint?: boolean;
  }

  export const DropdownToggle: (props: PropsWithChildren<DropdownToggleProps>) => JSX.Element;
}

declare module "cs2/input" {
  import type { NamedExoticComponent, PropsWithChildren } from "react";

  // Subset of the official toolchain declaration. Keyboard Escape / gamepad B arrive as the
  // "Back" UI input action through the game's input system, not as DOM keydown events.
  export type Action = () => void | boolean;
  export interface InputActionsDefinition {
    Back: Action;
  }
  export type InputAction = keyof InputActionsDefinition;
  export type InputActions = {
    [K in InputAction]?: InputActionsDefinition[K] | { actionContext?: string; onAction: InputActionsDefinition[K] | null } | null;
  };
  export interface InputActionConsumerProps {
    actions: InputActions | null;
    actionContext?: string;
    disabled?: boolean;
    ignoreFocusState?: boolean;
  }
  export const InputActionConsumer: NamedExoticComponent<PropsWithChildren<InputActionConsumerProps>>;
}
