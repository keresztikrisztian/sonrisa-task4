export interface DemoIdentity { id: string; displayName: string; role: 'User' | 'Admin'; }
export interface PageResult<T> { items: T[]; page: number; pageSize: number; totalCount: number; }
export interface AlertRule { id: number; name: string; category: string | null; keywords: string[]; channels: string[]; isEnabled: boolean; createdAtUtc: string; updatedAtUtc: string; }
export interface AlertRuleInput { name: string; category: string | null; keywords: string[]; channels: string[]; isEnabled: boolean; }
export interface EventItem { id: number; category: string; title: string; description: string; occurredAtUtc: string; createdAtUtc: string; }
export interface EventDetail extends EventItem { deliveries: Delivery[]; }
export interface EventSubmission extends EventItem { isDuplicate: boolean; }
export interface EventInput { category: string; title: string; description: string; occurredAt: string; }
export interface Delivery { id: number; eventId: number; alertRuleId: number; channel: string; status: string; attemptCount: number; createdAtUtc: string; updatedAtUtc: string; sentAtUtc: string | null; }