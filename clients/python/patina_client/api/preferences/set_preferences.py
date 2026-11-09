from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.server_preferences import ServerPreferences
from typing import cast



def _get_kwargs(
    *,
    body: ServerPreferences,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "put",
        "url": "/api/v1/preferences",
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> ProblemDetails | ServerPreferences | None:
    if response.status_code == 200:
        response_200 = ServerPreferences.from_dict(response.json())



        return response_200

    if response.status_code == 400:
        response_400 = ProblemDetails.from_dict(response.json())



        return response_400

    if response.status_code == 409:
        response_409 = ProblemDetails.from_dict(response.json())



        return response_409

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Response[ProblemDetails | ServerPreferences]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: ServerPreferences,

) -> Response[ProblemDetails | ServerPreferences]:
    """ Changes the person's settings, and keeps them in their preferences file.

    Args:
        body (ServerPreferences): The Server's settings that belong to the person and not to a
            library or project, kept in a plain file beside
            the discovery file, which anyone can read and edit.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ServerPreferences]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    body: ServerPreferences,

) -> ProblemDetails | ServerPreferences | None:
    """ Changes the person's settings, and keeps them in their preferences file.

    Args:
        body (ServerPreferences): The Server's settings that belong to the person and not to a
            library or project, kept in a plain file beside
            the discovery file, which anyone can read and edit.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ServerPreferences
     """


    return sync_detailed(
        client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: ServerPreferences,

) -> Response[ProblemDetails | ServerPreferences]:
    """ Changes the person's settings, and keeps them in their preferences file.

    Args:
        body (ServerPreferences): The Server's settings that belong to the person and not to a
            library or project, kept in a plain file beside
            the discovery file, which anyone can read and edit.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ServerPreferences]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    body: ServerPreferences,

) -> ProblemDetails | ServerPreferences | None:
    """ Changes the person's settings, and keeps them in their preferences file.

    Args:
        body (ServerPreferences): The Server's settings that belong to the person and not to a
            library or project, kept in a plain file beside
            the discovery file, which anyone can read and edit.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ServerPreferences
     """


    return (await asyncio_detailed(
        client=client,
body=body,

    )).parsed
