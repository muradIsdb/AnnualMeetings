import apiClient from './client';

export interface HotelOption {
  id: string;
  name: string;
  isActive: boolean;
  displayOrder: number;
}

export interface PickupDayOption {
  id: string;
  label: string;
  value: string;
  isActive: boolean;
  displayOrder: number;
}

export interface PickupHourOption {
  id: string;
  label: string;
  value: string;
  isActive: boolean;
  displayOrder: number;
}

// ─── Hotels ───────────────────────────────────────────────────────────────────
export const getHotels = () =>
  apiClient.get<HotelOption[]>('/settings/hotels').then(r => r.data);

export const getAllHotels = () =>
  apiClient.get<HotelOption[]>('/settings/hotels/all').then(r => r.data);

export const createHotel = (data: Omit<HotelOption, 'id'>) =>
  apiClient.post<HotelOption>('/settings/hotels', data).then(r => r.data);

export const updateHotel = (id: string, data: Omit<HotelOption, 'id'>) =>
  apiClient.put<HotelOption>(`/settings/hotels/${id}`, data).then(r => r.data);

export const deleteHotel = (id: string) =>
  apiClient.delete(`/settings/hotels/${id}`);

// ─── Pickup Days ──────────────────────────────────────────────────────────────
export const getPickupDays = () =>
  apiClient.get<PickupDayOption[]>('/settings/pickup-days').then(r => r.data);

export const getAllPickupDays = () =>
  apiClient.get<PickupDayOption[]>('/settings/pickup-days/all').then(r => r.data);

export const createPickupDay = (data: Omit<PickupDayOption, 'id'>) =>
  apiClient.post<PickupDayOption>('/settings/pickup-days', data).then(r => r.data);

export const updatePickupDay = (id: string, data: Omit<PickupDayOption, 'id'>) =>
  apiClient.put<PickupDayOption>(`/settings/pickup-days/${id}`, data).then(r => r.data);

export const deletePickupDay = (id: string) =>
  apiClient.delete(`/settings/pickup-days/${id}`);

// ─── Pickup Hours ─────────────────────────────────────────────────────────────
export const getPickupHours = () =>
  apiClient.get<PickupHourOption[]>('/settings/pickup-hours').then(r => r.data);

export const getAllPickupHours = () =>
  apiClient.get<PickupHourOption[]>('/settings/pickup-hours/all').then(r => r.data);

export const createPickupHour = (data: Omit<PickupHourOption, 'id'>) =>
  apiClient.post<PickupHourOption>('/settings/pickup-hours', data).then(r => r.data);

export const updatePickupHour = (id: string, data: Omit<PickupHourOption, 'id'>) =>
  apiClient.put<PickupHourOption>(`/settings/pickup-hours/${id}`, data).then(r => r.data);

export const deletePickupHour = (id: string) =>
  apiClient.delete(`/settings/pickup-hours/${id}`);
