from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.job_event import JobEvent
from ...types import UNSET, Unset
from typing import cast



def _get_kwargs(
    *,
    after: int | Unset = 0,

) -> dict[str, Any]:
    

    

    params: dict[str, Any] = {}

    params["after"] = after


    params = {k: v for k, v in params.items() if v is not UNSET and v is not None}


    _kwargs: dict[str, Any] = {
        "method": "get",
        "url": "/api/v1/jobs/events",
        "params": params,
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> list[JobEvent] | None:
    if response.status_code == 200:
        response_200 = []
        _response_200 = response.json()
        for response_200_item_data in (_response_200):
            response_200_item = JobEvent.from_dict(response_200_item_data)



            response_200.append(response_200_item)

        return response_200

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Response[list[JobEvent]]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    after: int | Unset = 0,

) -> Response[list[JobEvent]]:
    """ The events after a number. /ws/jobs?since= sends the same events as they happen.

    Args:
        after (int | Unset):  Default: 0.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[list[JobEvent]]
     """


    kwargs = _get_kwargs(
        after=after,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    after: int | Unset = 0,

) -> list[JobEvent] | None:
    """ The events after a number. /ws/jobs?since= sends the same events as they happen.

    Args:
        after (int | Unset):  Default: 0.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        list[JobEvent]
     """


    return sync_detailed(
        client=client,
after=after,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    after: int | Unset = 0,

) -> Response[list[JobEvent]]:
    """ The events after a number. /ws/jobs?since= sends the same events as they happen.

    Args:
        after (int | Unset):  Default: 0.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[list[JobEvent]]
     """


    kwargs = _get_kwargs(
        after=after,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    after: int | Unset = 0,

) -> list[JobEvent] | None:
    """ The events after a number. /ws/jobs?since= sends the same events as they happen.

    Args:
        after (int | Unset):  Default: 0.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        list[JobEvent]
     """


    return (await asyncio_detailed(
        client=client,
after=after,

    )).parsed
